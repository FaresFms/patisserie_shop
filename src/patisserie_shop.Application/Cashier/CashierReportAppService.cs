using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence;
using Intelligence.Decisions;
using Intelligence.Entities;
using Inventory.BranchInventory;
using Inventory.Entities;
using Microsoft.AspNetCore.Authorization;
using Operations.Cashier;
using Operations;
using Operations.Permissions;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace patisserie_shop.Cashier;

/// <summary>
/// Records manual low-stock reports raised by a cashier on the POS. A thin host-level
/// orchestrator: it resolves the product and branch-inventory through the SAME app
/// repositories, then inserts a Pending
/// <see cref="AppDecisionLog"/> of type <see cref="DecisionTypes.StockReport"/>. The
/// insert fires DecisionMadeEto, which lights up the manager's decision bell exactly like
/// a rule-engine decision.
///
/// The report carries the sentinel rule id
/// (<see cref="IntelligenceConstants.CashierReportRuleId"/>) because AppDecisionLog.RuleId
/// is non-null and there is no real rule behind a manual report. The decision is
/// informational — the manager acknowledges or dismisses it; there is no corrective
/// document to execute.
///
/// Gated by <see cref="OperationsPermissions.Cashier.ReportLowStock"/> so cashiers can
/// report stock without receiving broad Inventory permissions.
/// </summary>
[Authorize(OperationsPermissions.Cashier.ReportLowStock)]
public class CashierReportAppService : patisserie_shopAppService, ICashierReportAppService
{
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IBranchInventoryRepository _branchInventoryRepository;
    private readonly IRepository<AppDecisionLog, Guid> _decisionLogRepository;

    public CashierReportAppService(
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IBranchInventoryRepository branchInventoryRepository,
        IRepository<AppDecisionLog, Guid> decisionLogRepository)
    {
        _productRepository = productRepository;
        _branchRepository = branchRepository;
        _branchInventoryRepository = branchInventoryRepository;
        _decisionLogRepository = decisionLogRepository;
    }

    public async Task ReportLowStockAsync(ReportLowStockInput input)
    {
        EnsureBranchAllowed(input.BranchId);

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

        var product = await _productRepository.GetAsync(input.ProductId);
        var inventory = await _branchInventoryRepository.FindByBranchAndProductAsync(
            input.BranchId,
            input.ProductId);
        var qty = inventory?.Inventory.QuantityOnHand ?? 0;

        var branchName = await ResolveBranchNameAsync(input.BranchId) ?? input.BranchId.ToString();

        var reasoning = L["CashierReport:Reasoning", product.Name, branchName, qty];

        var log = new AppDecisionLog(
            id: GuidGenerator.Create(),
            ruleId: IntelligenceConstants.CashierReportRuleId,
            productId: input.ProductId,
            branchId: input.BranchId,
            decisionType: DecisionTypes.StockReport,
            reasoning: reasoning,
            suggestedAction: L["CashierReport:SuggestedAction"],
            stockAtEvaluation: qty);

        // autoSave so the constructor's DecisionMadeEto is dispatched and the manager bell
        // updates immediately.
        await _decisionLogRepository.InsertAsync(log, autoSave: true);
    }

    public async Task<List<Guid>> GetPendingLowStockProductIdsAsync(Guid branchId)
    {
        EnsureBranchAllowed(branchId);

        var pendingReports = await _decisionLogRepository.GetListAsync(l =>
            l.DecisionType == DecisionTypes.StockReport
            && l.BranchId == branchId
            && l.Status == DecisionLogStatuses.Pending);

        return pendingReports
            .Select(l => l.ProductId)
            .Distinct()
            .ToList();
    }

    private void EnsureBranchAllowed(Guid branchId)
    {
        var raw = CurrentUser.FindClaimValue(CashierClaimTypes.AssignedBranchId);
        if (Guid.TryParse(raw, out var assignedBranchId) && assignedBranchId != branchId)
        {
            throw new BusinessException(OperationsErrorCodes.BranchNotAssignedToCashier)
                .WithData("BranchId", branchId);
        }
    }

    private async Task<string?> ResolveBranchNameAsync(Guid branchId)
    {
        var branch = await _branchRepository.FindAsync(branchId);
        return branch?.Name;
    }
}
