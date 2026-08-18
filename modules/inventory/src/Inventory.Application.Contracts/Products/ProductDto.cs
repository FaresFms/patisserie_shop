using System;
using Volo.Abp.Application.Dtos;

namespace Inventory.Products;

public class ProductDto : EntityDto<Guid>
{
    public Guid CategoryId { get; set; }
    public Guid? DefaultSupplierId { get; set; }
    public string Name { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public string SKU { get; set; } = null!;
    public string? Description { get; set; }
    public string? DescriptionAr { get; set; }
    public string? DescriptionEn { get; set; }
    public string Unit { get; set; } = null!;
    public string UnitAr { get; set; } = null!;
    public string UnitEn { get; set; } = null!;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int ReorderLevel { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }

    public string ProductType { get; set; } = Inventory.ProductTypes.FinishedGood;
    public bool IsSellable { get; set; } = true;
    public bool IsPurchasable { get; set; } = true;
    public bool IsProducible { get; set; }

    /// <summary>Null = non-perishable (no batch/expiry tracking).</summary>
    public int? ShelfLifeDays { get; set; }

    public DateTime CreationTime { get; set; }
}
