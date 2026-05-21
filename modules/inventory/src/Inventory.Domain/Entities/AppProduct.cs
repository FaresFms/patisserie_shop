using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

public class AppProduct : FullAuditedAggregateRoot<Guid>
{
    public Guid CategoryId { get; set; }
    public Guid? DefaultSupplierId { get; set; }
    public string Name { get; set; } = null!;
    public string SKU { get; set; } = null!;
    public string? Description { get; set; }
    public string Unit { get; set; } = null!;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int ReorderLevel { get; set; } = 5;
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    protected AppProduct() { }

    public AppProduct(
        Guid id,
        Guid categoryId,
        string name,
        string sku,
        string unit,
        Guid? defaultSupplierId = null,
        string? description = null,
        decimal costPrice = 0m,
        decimal salePrice = 0m,
        string currency = "USD",
        int reorderLevel = 5,
        string? imageUrl = null,
        bool isActive = true)
        : base(id)
    {
        CategoryId = categoryId;
        Name = name;
        SKU = sku;
        Unit = unit;
        DefaultSupplierId = defaultSupplierId;
        Description = description;
        CostPrice = costPrice;
        SalePrice = salePrice;
        Currency = currency;
        ReorderLevel = reorderLevel;
        ImageUrl = imageUrl;
        IsActive = isActive;
    }
}
