using System.ComponentModel.DataAnnotations;

namespace Inventory.Suppliers;

public class CreateSupplierDto
{
    [Required]
    [StringLength(128)]
    public string Name { get; set; } = null!;

    [StringLength(128)]
    public string? ContactPerson { get; set; }

    [StringLength(32)]
    public string? Phone { get; set; }

    [StringLength(256)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(512)]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
}
