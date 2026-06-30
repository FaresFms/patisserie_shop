using System;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Intelligence.Entities;
using Intelligence.Rules;
using Operations;
using Operations.PurchaseOrders;
using patisserie_shop.Decisions;
using patisserie_shop.EntityFrameworkCore.Testing;
using Shouldly;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Intelligence;

/// <summary>
/// Purchase-order consolidation: ConsolidateReordersAsync groups every PENDING
/// reorder-type decision (LowStockAlert / ReorderSuggestion / StockoutRisk) by
/// (supplier × destination branch) into ONE draft PO per group — one line per
/// distinct product (same product across decisions merges to the MAX quantity),
/// links every consolidated decision to the PO, and skips (counts) decisions whose
/// product has no default supplier without failing the batch.
/// </summary>
[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class ConsolidationTests : IntegrationTestBase
{
    /// <summary>
    /// Drives a pending LowStockAlert decision for one product at one branch via the
    /// real stock-change → decision-engine path. Maximum stock is set so the fallback
    /// refill formula (no velocity) yields a deterministic order quantity.
    /// </summary>
    private async Task<Guid> ArrangePendingLowStockAsync(
        Guid branchId,
        Guid? defaultSupplierId,
        Guid categoryId,
        int onHand = 6,
        int maximumStock = 40)
    {
        var product = await CreateProductAsync(
            categoryId,
            defaultSupplierId: defaultSupplierId,
            costPrice: 2.50m);
        var inventory = await InitializeInventoryAsync(
            branchId, product.Id, quantity: 50, minimumStock: 8, maximumStock: maximumStock);
        await CreateRuleAsync(InventoryRuleTypes.LowStock, thresholdValue: 10, productId: product.Id);

        await AdjustStockAsync(inventory.Id, newQuantity: onHand);

        var decision = (await GetDecisionLogsAsync(product.Id))
            .Single(d => d.DecisionType == DecisionTypes.LowStockAlert);
        return decision.Id;
    }

    [Fact]
    public async Task Consolidates_Two_LowStock_Decisions_Same_Supplier_And_Branch_Into_One_PO()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var supplier = await CreateSupplierAsync();
        var branch = await CreateBranchAsync();

        // Two different products, same supplier + branch → ONE PO, TWO lines.
        var decision1 = await ArrangePendingLowStockAsync(branch.Id, supplier.Id, category.Id);
        var decision2 = await ArrangePendingLowStockAsync(branch.Id, supplier.Id, category.Id);

        var result = await GetRequiredService<IDecisionActionAppService>()
            .ConsolidateReordersAsync(new ConsolidateReordersInput());

        result.PurchaseOrdersCreated.ShouldBe(1);
        result.DecisionsConsolidated.ShouldBe(2);
        result.SkippedNoSupplier.ShouldBe(0);

        var consolidated = result.PurchaseOrders.ShouldHaveSingleItem();
        consolidated.LineCount.ShouldBe(2);
        consolidated.DecisionCount.ShouldBe(2);

        var po = await GetRequiredService<IPurchaseOrderAppService>().GetAsync(consolidated.PurchaseOrderId);
        po.Status.ShouldBe(PurchaseOrderStatuses.Draft);     // a human still approves
        po.SupplierId.ShouldBe(supplier.Id);
        po.DestBranchId.ShouldBe(branch.Id);
        po.Items.Count.ShouldBe(2);
        // Note wording is localized (Arabic in this build); the structural facts above/below
        // (1 PO, 2 lines, both decisions executed + linked) are the real contract.
        po.Notes.ShouldNotBeNullOrWhiteSpace();

        // Both decisions flipped to Executed and linked to the single PO.
        var logs = GetRequiredService<IDecisionLogAppService>();
        foreach (var id in new[] { decision1, decision2 })
        {
            var log = await logs.GetAsync(id);
            log.Status.ShouldBe(DecisionLogStatuses.Executed);
            log.ExecutedActionType.ShouldBe(DecisionActionTypes.PurchaseOrder);
            log.ExecutedActionId.ShouldBe(po.Id);
        }
    }

    [Fact]
    public async Task Consolidates_Two_Decisions_Different_Suppliers_Into_Two_POs()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var supplierA = await CreateSupplierAsync();
        var supplierB = await CreateSupplierAsync();
        var branch = await CreateBranchAsync();

        var decisionA = await ArrangePendingLowStockAsync(branch.Id, supplierA.Id, category.Id);
        var decisionB = await ArrangePendingLowStockAsync(branch.Id, supplierB.Id, category.Id);

        var result = await GetRequiredService<IDecisionActionAppService>()
            .ConsolidateReordersAsync(new ConsolidateReordersInput());

        result.PurchaseOrdersCreated.ShouldBe(2);
        result.DecisionsConsolidated.ShouldBe(2);
        result.SkippedNoSupplier.ShouldBe(0);

        // One PO per supplier, each with a single line.
        result.PurchaseOrders.Select(p => p.SupplierName).Distinct().Count().ShouldBe(2);
        result.PurchaseOrders.ShouldAllBe(p => p.LineCount == 1 && p.DecisionCount == 1);

        var logs = GetRequiredService<IDecisionLogAppService>();
        (await logs.GetAsync(decisionA)).Status.ShouldBe(DecisionLogStatuses.Executed);
        (await logs.GetAsync(decisionB)).Status.ShouldBe(DecisionLogStatuses.Executed);
    }

    [Fact]
    public async Task Skips_And_Counts_Decisions_Whose_Product_Has_No_Default_Supplier()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var supplier = await CreateSupplierAsync();
        var branch = await CreateBranchAsync();

        // One product WITH a supplier (gets a PO), one WITHOUT (skipped + counted).
        var withSupplier = await ArrangePendingLowStockAsync(branch.Id, supplier.Id, category.Id);
        var noSupplier = await ArrangePendingLowStockAsync(branch.Id, null, category.Id);

        var result = await GetRequiredService<IDecisionActionAppService>()
            .ConsolidateReordersAsync(new ConsolidateReordersInput());

        result.SkippedNoSupplier.ShouldBe(1);
        result.PurchaseOrdersCreated.ShouldBe(1);
        result.DecisionsConsolidated.ShouldBe(1);

        var logs = GetRequiredService<IDecisionLogAppService>();
        // The supplied product was still processed; the no-supplier one stays Pending.
        (await logs.GetAsync(withSupplier)).Status.ShouldBe(DecisionLogStatuses.Executed);
        (await logs.GetAsync(noSupplier)).Status.ShouldBe(DecisionLogStatuses.Pending);
    }

    [Fact]
    public async Task Merges_Two_Decision_Types_For_Same_Product_Into_One_Line_With_Max_Quantity()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var supplier = await CreateSupplierAsync();
        var branch = await CreateBranchAsync();

        var product = await CreateProductAsync(
            category.Id, defaultSupplierId: supplier.Id, costPrice: 2.50m);
        // Max 40, on hand 6, no velocity → fallback refill 40 − 6 = 34 for BOTH decisions.
        var inventory = await InitializeInventoryAsync(
            branch.Id, product.Id, quantity: 50, minimumStock: 8, maximumStock: 40);
        await CreateRuleAsync(InventoryRuleTypes.LowStock, thresholdValue: 10, productId: product.Id);
        await AdjustStockAsync(inventory.Id, newQuantity: 6);

        var lowStock = (await GetDecisionLogsAsync(product.Id))
            .Single(d => d.DecisionType == DecisionTypes.LowStockAlert);

        // A second decision of a DIFFERENT reorder type on the SAME product+branch.
        var reorder = await InsertDecisionLogAsync(new AppDecisionLog(
            Guid.NewGuid(),
            ruleId: lowStock.RuleId,
            productId: product.Id,
            branchId: branch.Id,
            decisionType: DecisionTypes.ReorderSuggestion,
            reasoning: "Manual reorder suggestion for the same product.",
            stockAtEvaluation: 6));

        var result = await GetRequiredService<IDecisionActionAppService>()
            .ConsolidateReordersAsync(new ConsolidateReordersInput());

        // One PO, ONE merged line (same product), but BOTH decisions linked.
        result.PurchaseOrdersCreated.ShouldBe(1);
        result.DecisionsConsolidated.ShouldBe(2);

        var consolidated = result.PurchaseOrders.ShouldHaveSingleItem();
        consolidated.LineCount.ShouldBe(1);
        consolidated.DecisionCount.ShouldBe(2);

        var po = await GetRequiredService<IPurchaseOrderAppService>().GetAsync(consolidated.PurchaseOrderId);
        var line = po.Items.ShouldHaveSingleItem();
        line.ProductId.ShouldBe(product.Id);
        line.OrderedQuantity.ShouldBe(34);   // max of the two identical fallback quantities

        var logs = GetRequiredService<IDecisionLogAppService>();
        var lowStockLog = await logs.GetAsync(lowStock.Id);
        var reorderLog = await logs.GetAsync(reorder.Id);
        lowStockLog.Status.ShouldBe(DecisionLogStatuses.Executed);
        reorderLog.Status.ShouldBe(DecisionLogStatuses.Executed);
        lowStockLog.ExecutedActionId.ShouldBe(po.Id);
        reorderLog.ExecutedActionId.ShouldBe(po.Id);
    }
}
