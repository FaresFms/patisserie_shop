using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

public class AppProduct : FullAuditedAggregateRoot<Guid>
{
    public Guid CategoryId { get; private set; }
    public Guid? DefaultSupplierId { get; private set; }
    public string Name { get; private set; } = null!;
    public string SKU { get; internal set; } = null!;
    public string? Description { get; private set; }
    public string Unit { get; private set; } = null!;
    public decimal CostPrice { get; private set; }
    public decimal SalePrice { get; private set; }
    public string Currency { get; private set; } = "USD";
    public int ReorderLevel { get; private set; } = 5;
    public string? ImageUrl { get; private set; }
    public bool IsActive { get; private set; } = true;

    protected AppProduct() { }

    internal AppProduct(
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
        SKU = Check.NotNullOrWhiteSpace(sku, nameof(sku)).Trim();
        UpdateInfo(
            categoryId,
            defaultSupplierId,
            name,
            unit,
            description,
            costPrice,
            salePrice,
            currency,
            reorderLevel,
            imageUrl,
            isActive);
    }

    public void UpdateInfo(
        Guid categoryId,
        Guid? defaultSupplierId,
        string name,
        string unit,
        string? description,
        decimal costPrice,
        decimal salePrice,
        string currency,
        int reorderLevel,
        string? imageUrl,
        bool isActive)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.NotNullOrWhiteSpace(unit, nameof(unit));
        Check.NotNullOrWhiteSpace(currency, nameof(currency));
        if (costPrice < 0) throw new ArgumentOutOfRangeException(nameof(costPrice));
        if (salePrice < 0) throw new ArgumentOutOfRangeException(nameof(salePrice));
        if (reorderLevel < 0) throw new ArgumentOutOfRangeException(nameof(reorderLevel));

        CategoryId = categoryId;
        DefaultSupplierId = defaultSupplierId;
        Name = name.Trim();
        Unit = unit.Trim();
        Description = description;
        CostPrice = costPrice;
        SalePrice = salePrice;
        Currency = currency.Trim();
        ReorderLevel = reorderLevel;
        ImageUrl = imageUrl;
        IsActive = isActive;
    }
}
