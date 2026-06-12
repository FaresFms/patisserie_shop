using System;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Intelligence.Entities;
using Intelligence.Rules;
using patisserie_shop.EntityFrameworkCore.Testing;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Intelligence;

/// <summary>
/// The deterministic learning loop: ~48h after a decision is raised the
/// DecisionOutcomeScanner compares it against the CURRENT stock situation and
/// records the write-once Outcome (Resolved / Unresolved / StockedOut).
/// Decisions are back-dated by pre-setting CreationTime before insert.
/// </summary>
[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class DecisionOutcomeScannerTests : IntegrationTestBase
{
    private async Task<(AppDecisionLog Decision, Guid InventoryId)> ArrangeBackdatedLowStockDecisionAsync(
        int currentStock)
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory = await InitializeInventoryAsync(branch.Id, product.Id, quantity: currentStock);
        var rule = await CreateRuleAsync(InventoryRuleTypes.LowStock, thresholdValue: 10);

        var decision = await InsertDecisionLogAsync(
            new AppDecisionLog(
                Guid.NewGuid(),
                ruleId: rule.Id,
                productId: product.Id,
                branchId: branch.Id,
                decisionType: DecisionTypes.LowStockAlert,
                reasoning: "Stock=6 is below threshold=10",
                stockAtEvaluation: 6),
            backdatedCreationTimeUtc: DateTime.UtcNow.AddDays(-3)); // past the 48h delay

        return (decision, inventory.Id);
    }

    private async Task RunScannerAsync()
    {
        var scanner = GetRequiredService<DecisionOutcomeScannerService>();
        await WithUnitOfWorkAsync(() => scanner.ScanAsync());
    }

    private async Task<AppDecisionLog> ReloadAsync(Guid decisionId)
    {
        var repository = GetRequiredService<IRepository<AppDecisionLog, Guid>>();
        return await WithUnitOfWorkAsync(() => repository.GetAsync(decisionId));
    }

    [Fact]
    public async Task Recovered_Stock_Marks_The_Decision_Resolved()
    {
        var (decision, inventoryId) = await ArrangeBackdatedLowStockDecisionAsync(currentStock: 6);
        await AdjustStockAsync(inventoryId, newQuantity: 25); // recovered above the threshold of 10

        await RunScannerAsync();

        var evaluated = await ReloadAsync(decision.Id);
        evaluated.Outcome.ShouldBe(DecisionOutcomes.Resolved);
        evaluated.OutcomeEvaluatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_Stockout_Marks_The_Decision_StockedOut_And_Still_Low_Stays_Unresolved()
    {
        // Stock fell to zero after the alert → the worst case the alert predicted.
        var (stockedOut, _) = await ArrangeBackdatedLowStockDecisionAsync(currentStock: 0);
        await RunScannerAsync();
        (await ReloadAsync(stockedOut.Id)).Outcome.ShouldBe(DecisionOutcomes.StockedOut);

        // Fresh app per test class instance, so arrange the second scenario separately:
        // stock still at 6 (≤ threshold 10, but not zero) → Unresolved.
        var (stillLow, _) = await ArrangeBackdatedLowStockDecisionAsync(currentStock: 6);
        await RunScannerAsync();
        (await ReloadAsync(stillLow.Id)).Outcome.ShouldBe(DecisionOutcomes.Unresolved);
    }

    [Fact]
    public async Task Decisions_Younger_Than_48h_Are_Not_Evaluated()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        await InitializeInventoryAsync(branch.Id, product.Id, quantity: 50);
        var rule = await CreateRuleAsync(InventoryRuleTypes.LowStock, thresholdValue: 10);

        var young = await InsertDecisionLogAsync(new AppDecisionLog(
            Guid.NewGuid(),
            ruleId: rule.Id,
            productId: product.Id,
            branchId: branch.Id,
            decisionType: DecisionTypes.LowStockAlert,
            reasoning: "Fresh decision — manager still has time to act.",
            stockAtEvaluation: 6)); // CreationTime = now

        await RunScannerAsync();

        (await ReloadAsync(young.Id)).Outcome.ShouldBeNull();
    }
}
