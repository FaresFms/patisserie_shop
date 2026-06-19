using System;
using System.ComponentModel.DataAnnotations;

namespace patisserie_shop.Cashier;

/// <summary>
/// Input for creating a brand-new cashier user: full identity details plus the branch
/// the cashier is assigned to. The created user is placed in the Cashier role and gets a
/// persistent <c>AssignedBranchId</c> claim. A manager may only target a branch they
/// manage (enforced server-side). The branch assignment takes effect on first login.
/// </summary>
public class CreateCashierInput
{
    [Required]
    [StringLength(256, MinimumLength = 3)]
    public string UserName { get; set; } = null!;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string Password { get; set; } = null!;

    [Required]
    [StringLength(64)]
    public string Name { get; set; } = null!;

    [StringLength(64)]
    public string? Surname { get; set; }

    [StringLength(32)]
    public string? PhoneNumber { get; set; }

    [Required]
    public Guid BranchId { get; set; }
}
