using System.Linq;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Intelligence.Rules;
using patisserie_shop.EntityFrameworkCore.Testing;
using Shouldly;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Intelligence;

/// <summary>
/// The thesis-core event chain, end to end against the real EF stack:
/// stock adjustment → AppBranchInventory.UpdateStock → StockChangedEto →
/// StockChangedEventHandler → DecisionMakerService → AppDecisionLog row.
/// </summary>
[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class DecisionChainTests : IntegrationTestBase
{
    [Fact]
    public async Task Stock_Drop_Below_Threshold_Creates_A_Pending_LowStock_Decision()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory = await InitializeInventoryAsync(branch.Id, product.Id, quantity: 50);
        var rule = await CreateRuleAsync(InventoryRuleTypes.LowStock, thresholdValue: 10);

        await AdjustStockAsync(inventory.Id, newQuantity: 6);

        var log = (await GetDecisionLogsAsync(product.Id)).ShouldHaveSingleItem();
        log.DecisionType.ShouldBe(DecisionTypes.LowStockAlert);
        log.Status.ShouldBe(DecisionLogStatuses.Pending);
        log.RuleId.ShouldBe(rule.Id);
        log.BranchId.ShouldBe(branch.Id);
        log.StockAtEvaluation.ShouldBe(6);
        log.Reasoning.ShouldContain("below threshold=10");
    }

    [Fact]
    public async Task A_Second_Drop_Does_Not_Duplicate_The_Open_Pending_Decision()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory = await InitializeInventoryAsync(branch.Id, product.Id, quantity: 50);
        await CreateRuleAsync(InventoryRuleTypes.LowStock, thresholdValue: 10);

        await AdjustStockAsync(inventory.Id, newQuantity: 6);
        await AdjustStockAsync(inventory.Id, newQuantity: 4); // still below threshold

        var logs = await GetDecisionLogsAsync(product.Id);
        logs.Count.ShouldBe(1); // the open Pending alert suppresses the duplicate
        logs[0].StockAtEvaluation.ShouldBe(6); // the original evaluation is preserved
    }

    [Fact]
    public async Task Stock_Above_Ceiling_Creates_An_ExcessStock_Decision_But_Not_Below_It()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory = await InitializeInventoryAsync(branch.Id, product.Id, quantity: 50);
        await CreateRuleAsync(InventoryRuleTypes.ExcessStock, thresholdValue: 100);

        await AdjustStockAsync(inventory.Id, newQuantity: 90); // under the ceiling → nothing
        (await GetDecisionLogsAsync(product.Id)).ShouldBeEmpty();

        await AdjustStockAsync(inventory.Id, newQuantity: 150);

        var log = (await GetDecisionLogsAsync(product.Id)).ShouldHaveSingleItem();
        log.DecisionType.ShouldBe(DecisionTypes.ExcessStockAlert);
        log.Status.ShouldBe(DecisionLogStatuses.Pending);
        log.StockAtEvaluation.ShouldBe(150);
    }

    [Fact]
    public async Task Highest_Priority_Rule_Wins_When_Several_LowStock_Rules_Match()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory = await InitializeInventoryAsync(branch.Id, product.Id, quantity: 50);

        await CreateRuleAsync(InventoryRuleTypes.LowStock, thresholdValue: 10, priority: 0);
        var critical = await CreateRuleAsync(InventoryRuleTypes.LowStock, thresholdValue: 10, priority: 10);

        await AdjustStockAsync(inventory.Id, newQuantity: 3); // both rules match

        // One alert, attributed to the higher-priority rule (its message wins).
        var log = (await GetDecisionLogsAsync(product.Id)).ShouldHaveSingleItem();
        log.RuleId.ShouldBe(critical.Id);
    }

    [Fact]
    public async Task Scoped_Rule_For_Another_Product_Does_Not_Fire()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var watched = await CreateProductAsync(category.Id);
        var other = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var watchedInventory = await InitializeInventoryAsync(branch.Id, watched.Id, quantity: 50);
        var otherInventory = await InitializeInventoryAsync(branch.Id, other.Id, quantity: 50);

        // Product-scoped rule for ONE product only.
        await CreateRuleAsync(InventoryRuleTypes.LowStock, thresholdValue: 10, productId: watched.Id);

        await AdjustStockAsync(otherInventory.Id, newQuantity: 2);   // out of scope → silent
        await AdjustStockAsync(watchedInventory.Id, newQuantity: 2); // in scope → alert

        (await GetDecisionLogsAsync(other.Id)).ShouldBeEmpty();
        (await GetDecisionLogsAsync(watched.Id)).ShouldHaveSingleItem();
    }
}
