using System;
using System.ComponentModel.DataAnnotations;

namespace Inventory.Products;

public class CreateProductDto
{
    [Required]
    public Guid CategoryId { get; set; }

    public Guid? DefaultSupplierId { get; set; }

    [Required]
    [StringLength(128)]
    public string NameAr { get; set; } = null!;

    [Required]
    [StringLength(128)]
    public string NameEn { get; set; } = null!;

    [Required]
    [StringLength(64)]
    public string SKU { get; set; } = null!;

    [StringLength(1024)]
    public string? DescriptionAr { get; set; }

    [StringLength(1024)]
    public string? DescriptionEn { get; set; }

    [Required]
    [StringLength(32)]
    public string UnitAr { get; set; } = null!;

    [Required]
    [StringLength(32)]
    public string UnitEn { get; set; } = null!;

    [Range(0, double.MaxValue)]
    public decimal CostPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal SalePrice { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "USD";

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; } = 5;

    [StringLength(512)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    [StringLength(16)]
    public string ProductType { get; set; } = Inventory.ProductTypes.FinishedGood;

    public bool IsSellable { get; set; } = true;
    public bool IsPurchasable { get; set; } = true;
    public bool IsProducible { get; set; } = false;

    /// <summary>Null = non-perishable (no batch/expiry tracking).</summary>
    [Range(1, 3650)]
    public int? ShelfLifeDays { get; set; }
}
