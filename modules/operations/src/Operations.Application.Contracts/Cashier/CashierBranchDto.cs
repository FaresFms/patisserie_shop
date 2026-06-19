using System;

namespace Operations.Cashier;

/// <summary>
/// FROZEN CONTRACT — a lightweight branch entry for the cashier POS branch picker.
/// Returned by <see cref="ICashierAppService.GetSellableBranchesAsync"/> so the POS can
/// list branches WITHOUT requiring the Inventory Branches.Default permission (which would
/// otherwise add the Branches page to a cashier's menu). Id + Name only.
/// </summary>
public class CashierBranchDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}
