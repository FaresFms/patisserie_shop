using System;
using System.ComponentModel.DataAnnotations;

namespace patisserie_shop.Cashier;

/// <summary>
/// Assigns a single branch to a cashier. The assignment is stored as the persistent
/// <c>AssignedBranchId</c> claim on the user and takes effect on the cashier's next login.
/// </summary>
public class AssignCashierBranchInput
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid BranchId { get; set; }
}
