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

    /// <summary>
    /// What this product is in the production/sales pipeline: <see cref="ProductTypes.FinishedGood"/>,
    /// <see cref="ProductTypes.RawMaterial"/>, <see cref="ProductTypes.Packaging"/> or
    /// <see cref="ProductTypes.SemiFinished"/>.
    /// </summary>
    public string ProductType { get; private set; } = ProductTypes.FinishedGood;

    /// <summary>Whether this product can be sold at the POS / on a sale.</summary>
    public bool IsSellable { get; private set; } = true;

    /// <summary>Whether this product can be bought from a supplier on a purchase order.</summary>
    public bool IsPurchasable { get; private set; } = true;

    /// <summary>Whether this product can be made by a production order.</summary>
    public bool IsProducible { get; private set; } = false;

    /// <summary>
    /// Days the product stays sellable after it is received. Null = non-perishable —
    /// no stock-batch (expiry) tracking happens for this product.
    /// </summary>
    public int? ShelfLifeDays { get; private set; }

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
        bool isActive = true,
        int? shelfLifeDays = null,
        string productType = ProductTypes.FinishedGood,
        bool isSellable = true,
        bool isPurchasable = true,
        bool isProducible = false)
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
            isActive,
            shelfLifeDays,
            productType,
            isSellable,
            isPurchasable,
            isProducible);
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
        bool isActive,
        int? shelfLifeDays = null,
        string productType = ProductTypes.FinishedGood,
        bool isSellable = true,
        bool isPurchasable = true,
        bool isProducible = false)
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
        SetShelfLifeDays(shelfLifeDays);
        SetClassification(productType, isSellable, isPurchasable, isProducible);
    }

    public void SetClassification(string productType, bool isSellable, bool isPurchasable, bool isProducible)
    {
        if (!ProductTypes.IsValid(productType))
        {
            throw new BusinessException(InventoryErrorCodes.InvalidProductType)
                .WithData("ProductType", productType);
        }

        ProductType = productType;
        IsSellable = isSellable;
        IsPurchasable = isPurchasable;
        IsProducible = isProducible;
    }

    /// <summary>Null clears perishability (no batch tracking); when set, 1–3650 days.</summary>
    public void SetShelfLifeDays(int? shelfLifeDays)
    {
        if (shelfLifeDays.HasValue && (shelfLifeDays.Value < 1 || shelfLifeDays.Value > 3650))
        {
            throw new BusinessException(InventoryErrorCodes.InvalidShelfLifeDays)
                .WithData("ShelfLifeDays", shelfLifeDays.Value);
        }

        ShelfLifeDays = shelfLifeDays;
    }
}
