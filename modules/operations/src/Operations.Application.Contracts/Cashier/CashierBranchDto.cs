using System;

namespace Operations.Cashier;

/// <summary>
/// Lightweight branch data for the cashier POS. The address is used on receipts so each
/// invoice shows the location of the branch where the sale was recorded.
/// </summary>
public class CashierBranchDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
}
