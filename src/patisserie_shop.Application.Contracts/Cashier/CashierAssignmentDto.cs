using System;

namespace patisserie_shop.Cashier;

/// <summary>
/// A cashier user row for the branch-assignment admin screen: the user's identity plus
/// the branch currently assigned via their persistent <c>AssignedBranchId</c> claim
/// (null when unassigned).
/// </summary>
public class CashierAssignmentDto
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = null!;

    public string? Email { get; set; }

    public Guid? AssignedBranchId { get; set; }

    public string? AssignedBranchName { get; set; }
}
