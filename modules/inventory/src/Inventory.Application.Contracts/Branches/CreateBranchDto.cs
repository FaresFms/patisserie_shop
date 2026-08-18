using System;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Branches;

public class CreateBranchDto
{
    [Required, StringLength(128)]
    public string NameAr { get; set; } = null!;
    [Required, StringLength(128)]
    public string NameEn { get; set; } = null!;
    [StringLength(512)]
    public string? AddressAr { get; set; }
    [StringLength(512)]
    public string? AddressEn { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Guid? ManagerUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public string BranchType { get; set; } = Inventory.BranchTypes.SalesBranch;
}
