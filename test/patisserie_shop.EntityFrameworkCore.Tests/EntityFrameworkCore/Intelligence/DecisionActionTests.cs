using System;
using System.Linq;
using System.Threading.Tasks;
using Intelligence;
using Intelligence.Decisions;
using Intelligence.Entities;
using Intelligence.Rules;
using Operations;
using Operations.PurchaseOrders;
using Operations.StockTransfers;
using patisserie_shop.Decisions;
using patisserie_shop.EntityFrameworkCore.Testing;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Intelligence;

/// <summary>
/// The decision → action loop: DecisionActionAppService.ExecuteDecisionAsync turns a
/// Pending decision into a DRAFT corrective document (PO or transfer), records the
/// document link on the ledger row, and flips it to Executed.
/// </summary>
[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class DecisionActionTests : IntegrationTestBase
{
    private async Task<(Guid DecisionId, Guid ProductId, Guid BranchId, Guid SupplierId)> ArrangeLowStockDecisionAsync(
        bool withDefaultSupplier = true,
        int? maximumStock = null,
        int supplierLeadTimeDays = 3)
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var supplier = await CreateSupplierAsync(leadTimeDays: supplierLeadTimeDays);
        var product = await CreateProductAsync(
            category.Id,
            defaultSupplierId: withDefaultSupplier ? supplier.Id : null,
            costPrice: 2.50m);
        var branch = await CreateBranchAsync();
        var inventory = await InitializeInventoryAsync(
            branch.Id, product.Id, quantity: 50, minimumStock: 8, maximumStock: maximumStock);
        await CreateRuleAsync(InventoryRuleTypes.LowStock, thresholdValue: 10);

        await AdjustStockAsync(inventory.Id, newQuantity: 6);

        var decision = (await GetDecisionLogsAsync(product.Id)).ShouldHaveSingleItem();
        return (decision.Id, product.Id, branch.Id, supplier.Id);
    }

    [Fact]
    public async Task Executing_A_LowStock_Decision_Creates_A_Draft_PO_With_The_Fallback_Refill_Quantity()
    {
        // MaximumStock 40, on hand 6, no velocity data → Phase 1 refill: 40 − 6 = 34.
        var (decisionId, productId, branchId, supplierId) =
            await ArrangeLowStockDecisionAsync(maximumStock: 40);

        var result = await GetRequiredService<IDecisionActionAppService>()
            .ExecuteDecisionAsync(decisionId);

        result.ActionCreated.ShouldBeTrue();
        result.ActionType.ShouldBe(DecisionActionTypes.PurchaseOrder);

        var po = await GetRequiredService<IPurchaseOrderAppService>().GetAsync(result.ActionId!.Value);
        po.Status.ShouldBe(PurchaseOrderStatuses.Draft);     // human still approves
        po.SupplierId.ShouldBe(supplierId);                  // product's default supplier
        po.DestBranchId.ShouldBe(branchId);
        var line = po.Items.ShouldHaveSingleItem();
        line.ProductId.ShouldBe(productId);
        line.OrderedQuantity.ShouldBe(34);
        line.UnitPrice.ShouldBe(2.50m);                      // product cost price

        // Ledger row flipped exactly once, with the document link recorded.
        var decision = await GetRequiredService<IDecisionLogAppService>().GetAsync(decisionId);
        decision.Status.ShouldBe(DecisionLogStatuses.Executed);
        decision.ExecutedActionType.ShouldBe(DecisionActionTypes.PurchaseOrder);
        decision.ExecutedActionId.ShouldBe(po.Id);

        // Executing again must fail — the ledger row is no longer Pending.
        await Should.ThrowAsync<BusinessException>(() =>
            GetRequiredService<IDecisionActionAppService>().ExecuteDecisionAsync(decisionId));
    }

    [Fact]
    public async Task Executing_With_Velocity_Data_Uses_The_ReorderPoint_Formula()
    {
        // avg 2/day, lead time 3d → target = ceil(2×(3+7)) + ceil(2×2) = 24; on hand 6 → order 18.
        var (decisionId, productId, branchId, _) =
            await ArrangeLowStockDecisionAsync(supplierLeadTimeDays: 3);

        var velocityRepository = GetRequiredService<IRepository<AppProductVelocity, Guid>>();
        await WithUnitOfWorkAsync(async () =>
        {
            var velocity = new AppProductVelocity(Guid.NewGuid(), productId, branchId);
            velocity.UpdateMetrics(
                avgDaily7: 2m, avgDaily30: 2m, qtySold30: 60, revenue30: 300m,
                abcClass: "A", computedAtUtc: DateTime.UtcNow);
            await velocityRepository.InsertAsync(velocity);
        });

        var result = await GetRequiredService<IDecisionActionAppService>()
            .ExecuteDecisionAsync(decisionId);

        var po = await GetRequiredService<IPurchaseOrderAppService>().GetAsync(result.ActionId!.Value);
        // OrderedQuantity==18 IS the ROP-formula contract (avgDaily 2 × (lead+cover) + safety).
        // The note wording is localized (Arabic in this build), so assert it exists, not its text.
        po.Items.ShouldHaveSingleItem().OrderedQuantity.ShouldBe(18);
        po.Notes.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Executing_Without_A_Default_Supplier_Fails_And_Leaves_The_Decision_Pending()
    {
        var (decisionId, _, _, _) = await ArrangeLowStockDecisionAsync(withDefaultSupplier: false);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            GetRequiredService<IDecisionActionAppService>().ExecuteDecisionAsync(decisionId));
        ex.Code.ShouldBe(IntelligenceErrorCodes.DecisionProductHasNoDefaultSupplier);

        var decision = await GetRequiredService<IDecisionLogAppService>().GetAsync(decisionId);
        decision.Status.ShouldBe(DecisionLogStatuses.Pending); // nothing was half-executed
    }

    [Fact]
    public async Task Executing_A_TransferSuggestion_Creates_A_Draft_Transfer_From_Source_To_Target()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var source = await CreateBranchAsync();
        var target = await CreateBranchAsync();
        // Source: 50 on hand, min 10 → surplus 40. Target: 2 on hand, min 12 → deficit 10.
        await InitializeInventoryAsync(source.Id, product.Id, quantity: 50, minimumStock: 10);
        await InitializeInventoryAsync(target.Id, product.Id, quantity: 2, minimumStock: 12);
        var rule = await CreateRuleAsync(InventoryRuleTypes.TransferSuggestion, thresholdValue: 10);

        var decision = await InsertDecisionLogAsync(new AppDecisionLog(
            Guid.NewGuid(),
            ruleId: rule.Id,
            productId: product.Id,
            branchId: target.Id,
            decisionType: DecisionTypes.TransferSuggestion,
            reasoning: "Target branch is low while source has excess.",
            stockAtEvaluation: 2,
            sourceBranchId: source.Id,
            targetBranchId: target.Id));

        var result = await GetRequiredService<IDecisionActionAppService>()
            .ExecuteDecisionAsync(decision.Id);

        result.ActionType.ShouldBe(DecisionActionTypes.StockTransfer);
        var transfer = await GetRequiredService<IStockTransferAppService>().GetAsync(result.ActionId!.Value);
        transfer.Status.ShouldBe(StockTransferStatuses.Draft);
        transfer.FromBranchId.ShouldBe(source.Id);
        transfer.ToBranchId.ShouldBe(target.Id);
        // qty = min(max(deficit 10, surplus/2 = 20), surplus 40) = 20.
        var line = transfer.Items.ShouldHaveSingleItem();
        line.ProductId.ShouldBe(product.Id);
        line.RequestedQuantity.ShouldBe(20);

        var executed = await GetRequiredService<IDecisionLogAppService>().GetAsync(decision.Id);
        executed.Status.ShouldBe(DecisionLogStatuses.Executed);
        executed.ExecutedActionId.ShouldBe(transfer.Id);
    }
}
