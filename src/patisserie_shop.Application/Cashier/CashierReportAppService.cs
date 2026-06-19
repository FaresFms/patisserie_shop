using System;
using System.Threading.Tasks;
using Intelligence;
using Intelligence.Decisions;
using Intelligence.Entities;
using Inventory.BranchInventory;
using Inventory.Products;
using Microsoft.AspNetCore.Authorization;
using Operations.Cashier;
using Operations.Permissions;
using Volo.Abp.Domain.Repositories;

namespace patisserie_shop.Cashier;

/// <summary>
/// Records manual low-stock reports raised by a cashier on the POS. A thin host-level
/// orchestrator: it resolves the product and branch-inventory through the SAME app
/// services <see cref="patisserie_shop.Decisions.DecisionActionAppService"/> uses
/// (products via <see cref="IProductAppService"/>, branch stock via
/// <see cref="IBranchInventoryAppService"/>), then inserts a Pending
/// <see cref="AppDecisionLog"/> of type <see cref="DecisionTypes.StockReport"/>. The
/// insert fires DecisionMadeEto, which lights up the manager's decision bell exactly like
/// a rule-engine decision.
///
/// The branch DISPLAY NAME comes from the cashier-permitted
/// <see cref="ICashierAppService.GetSellableBranchesAsync"/> (the cashier lacks Inventory's
/// Branches.Default), and the report carries the sentinel rule id
/// (<see cref="IntelligenceConstants.CashierReportRuleId"/>) because AppDecisionLog.RuleId
/// is non-null and there is no real rule behind a manual report. The decision is
/// informational — the manager acknowledges or dismisses it; there is no corrective
/// document to execute.
///
/// Gated by <see cref="OperationsPermissions.Cashier.Default"/> — the same permission that
/// gates the POS itself.
/// </summary>
[Authorize(OperationsPermissions.Cashier.Default)]
public class CashierReportAppService : patisserie_shopAppService, ICashierReportAppService
{
    private readonly IProductAppService _productAppService;
    private readonly IBranchInventoryAppService _branchInventoryAppService;
    private readonly ICashierAppService _cashierAppService;
    private readonly IRepository<AppDecisionLog, Guid> _decisionLogRepository;

    public CashierReportAppService(
        IProductAppService productAppService,
        IBranchInventoryAppService branchInventoryAppService,
        ICashierAppService cashierAppService,
        IRepository<AppDecisionLog, Guid> decisionLogRepository)
    {
        _productAppService = productAppService;
        _branchInventoryAppService = branchInventoryAppService;
        _cashierAppService = cashierAppService;
        _decisionLogRepository = decisionLogRepository;
    }

    public async Task ReportLowStockAsync(ReportLowStockInput input)
    {
        // Dedup: one open report per product+branch. If the manager hasn't actioned the
        // last one yet, a second cashier press is a no-op rather than a duplicate alert.
        var alreadyOpen = await _decisionLogRepository.AnyAsync(l =>
            l.DecisionType == DecisionTypes.StockReport
            && l.ProductId == input.ProductId
            && l.BranchId == input.BranchId
            && l.Status == DecisionLogStatuses.Pending);
        if (alreadyOpen)
        {
            return;
        }

        // Resolve the product (name + SKU) and the current on-hand quantity via the same
        // app services DecisionActionAppService uses: products via IProductAppService,
        // branch inventory via IBranchInventoryAppService filtered by the unique SKU.
        var product = await _productAppService.GetAsync(input.ProductId);
        var inventory = await FindBranchInventoryAsync(input.BranchId, input.ProductId, product.SKU);
        var qty = inventory?.QuantityOnHand ?? 0;

        var branchName = await ResolveBranchNameAsync(input.BranchId) ?? input.BranchId.ToString();

        var reasoning =
            $"Cashier reported low stock on '{product.Name}' at '{branchName}' (on hand: {qty}).";

        var log = new AppDecisionLog(
            id: GuidGenerator.Create(),
            ruleId: IntelligenceConstants.CashierReportRuleId,
            productId: input.ProductId,
            branchId: input.BranchId,
            decisionType: DecisionTypes.StockReport,
            reasoning: reasoning,
            suggestedAction: "Restock",
            stockAtEvaluation: qty);

        // autoSave so the constructor's DecisionMadeEto is dispatched and the manager bell
        // updates immediately.
        await _decisionLogRepository.InsertAsync(log, autoSave: true);
    }

    /// <summary>
    /// Locates the branch-inventory row for a product via the existing list endpoint
    /// (filtered by the product's unique SKU). Returns null when the product was never
    /// initialized at that branch — the report still records with on-hand 0.
    /// </summary>
    private async Task<BranchInventoryDto?> FindBranchInventoryAsync(Guid branchId, Guid productId, string sku)
    {
        var page = await _branchInventoryAppService.GetListAsync(new GetBranchInventoryInput
        {
            BranchId = branchId,
            Filter = sku,
            IncludeInactiveProducts = true,
            SkipCount = 0,
            MaxResultCount = 100
        });

        foreach (var row in page.Items)
        {
            if (row.ProductId == productId)
            {
                return row;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the branch display name from the cashier-permitted sellable-branches list
    /// (the cashier lacks Inventory's Branches.Default). Returns null when the branch isn't
    /// in that list; the reasoning then falls back to the branch id.
    /// </summary>
    private async Task<string?> ResolveBranchNameAsync(Guid branchId)
    {
        var branches = await _cashierAppService.GetSellableBranchesAsync();
        foreach (var branch in branches)
        {
            if (branch.Id == branchId)
            {
                return branch.Name;
            }
        }

        return null;
    }
}
