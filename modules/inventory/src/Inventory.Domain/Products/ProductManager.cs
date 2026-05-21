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

    public ProductManager(IRepository<AppProduct, Guid> productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<AppProduct> CreateAsync(
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
    {
        await EnsureSkuIsUniqueAsync(sku);

        return new AppProduct(
            GuidGenerator.Create(),
            categoryId,
            name,
            sku.Trim(),
            unit,
            defaultSupplierId,
            description,
            costPrice,
            salePrice,
            currency,
            reorderLevel,
            imageUrl,
            isActive);
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
