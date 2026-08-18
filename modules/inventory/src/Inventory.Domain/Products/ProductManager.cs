using System;
using System.Threading.Tasks;
using Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Inventory.Products;

public class ProductManager : DomainService
{
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppCategory, Guid> _categoryRepository;
    private readonly IRepository<AppSupplier, Guid> _supplierRepository;

    public ProductManager(
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppCategory, Guid> categoryRepository,
        IRepository<AppSupplier, Guid> supplierRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<AppProduct> CreateAsync(
        Guid categoryId,
        string nameAr,
        string nameEn,
        string sku,
        string unitAr,
        string unitEn,
        Guid? defaultSupplierId = null,
        string? descriptionAr = null,
        string? descriptionEn = null,
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
    {
        await EnsureCategoryExistsAsync(categoryId);
        await EnsureSupplierExistsAsync(defaultSupplierId);
        await EnsureSkuIsUniqueAsync(sku);

        return new AppProduct(
            GuidGenerator.Create(),
            categoryId,
            nameAr,
            nameEn,
            sku.Trim(),
            unitAr,
            unitEn,
            defaultSupplierId,
            descriptionAr,
            descriptionEn,
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

    public async Task EnsureReferencesAsync(Guid categoryId, Guid? defaultSupplierId)
    {
        await EnsureCategoryExistsAsync(categoryId);
        await EnsureSupplierExistsAsync(defaultSupplierId);
    }

    private Task EnsureCategoryExistsAsync(Guid categoryId)
        => _categoryRepository.GetAsync(categoryId);

    private async Task EnsureSupplierExistsAsync(Guid? supplierId)
    {
        if (supplierId.HasValue)
        {
            await _supplierRepository.GetAsync(supplierId.Value);
        }
    }

    public async Task ChangeSkuAsync(AppProduct product, string newSku)
    {
        Check.NotNull(product, nameof(product));
        Check.NotNullOrWhiteSpace(newSku, nameof(newSku));

        var normalized = newSku.Trim();
        if (string.Equals(product.SKU, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await EnsureSkuIsUniqueAsync(normalized, product.Id);
        product.SKU = normalized;
    }

    private async Task EnsureSkuIsUniqueAsync(string sku, Guid? ignoreId = null)
    {
        Check.NotNullOrWhiteSpace(sku, nameof(sku));
        var normalized = sku.Trim();

        var exists = await _productRepository.AnyAsync(p =>
            p.SKU == normalized && (ignoreId == null || p.Id != ignoreId.Value));

        if (exists)
        {
            throw new BusinessException(InventoryErrorCodes.DuplicateProductSku)
                .WithData("SKU", normalized);
        }
    }
}
