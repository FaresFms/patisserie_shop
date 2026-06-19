using System;

namespace patisserie_shop.Cashier;

/// <summary>
/// A lightweight active-branch option (Id + Name) for the branch-assignment dropdown
/// on the cashier-assignments admin screen.
/// </summary>
public class CashierBranchOptionDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;
}
