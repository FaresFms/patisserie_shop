using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Operations.Cashier;

/// <summary>
/// FROZEN CONTRACT — Wave 2 (UI / role seeding / low-stock report) codes against the exact
/// method and DTO names here. Cash-only POS backend: cash-drawer shifts, sale recording at
/// server-set prices, recent-sales/void within a 60-minute window, and a manager drawer view.
/// </summary>
public interface ICashierAppService : IApplicationService
{
    /// <summary>The current user's Open shift at the branch with live drawer figures, or null.</summary>
    Task<CashierShiftDto?> GetCurrentShiftAsync(Guid branchId);

    /// <summary>Opens a cash-drawer session for the current cashier at the branch.</summary>
    Task<CashierShiftDto> OpenShiftAsync(OpenShiftDto input);

    /// <summary>Counts the drawer, computes expected cash from the shift's sales, and closes it.</summary>
    Task<CashierShiftDto> CloseShiftAsync(CloseShiftDto input);

    /// <summary>
    /// Sellable product tiles at the branch (active products initialised there). Never exposes
    /// cost or quantity — only sale price and coarse low/out-of-stock flags.
    /// </summary>
    Task<List<CashierProductDto>> GetProductTilesAsync(Guid branchId, string? filter);

    /// <summary>
    /// Rings up a cash sale. Unit price is set server-side from each product's SalePrice — the
    /// cashier cannot set price. Requires an Open shift for this cashier+branch.
    /// </summary>
    Task<CashierSaleResultDto> RecordSaleAsync(RecordCashierSaleDto input);

    /// <summary>This cashier's sales at the branch within the window (default 60 min), newest first.</summary>
    Task<List<RecentSaleDto>> GetRecentSalesAsync(Guid branchId, int withinMinutes = 60);

    /// <summary>
    /// Voids a sale and restores its stock. Within 60 minutes for the cashier; anytime for a
    /// caller with Sales.ManageAll.
    /// </summary>
    Task VoidSaleAsync(VoidSaleDto input);

    /// <summary>Manager drawer view: cashier shifts across (accessible) branches.</summary>
    Task<List<CashierShiftDto>> GetShiftsAsync(GetShiftsInput input);

    /// <summary>
    /// The single branch (Id + Name) the current cashier is assigned to via their
    /// persistent <c>AssignedBranchId</c> claim, or null when no (active) branch is
    /// assigned. The POS uses this instead of a branch picker — the assigned branch is
    /// the authoritative branch context for the cashier.
    /// </summary>
    Task<CashierBranchDto?> GetMyBranchAsync();

    /// <summary>
    /// Active branches (Id + Name). No longer the POS branch source (the POS uses the
    /// assigned-branch claim via <see cref="GetMyBranchAsync"/>); retained so the
    /// host's cashier-report service can resolve a branch display name without
    /// Inventory's Branches.Default permission. Gated by the cashier permission only.
    /// </summary>
    Task<List<CashierBranchDto>> GetSellableBranchesAsync();
}
