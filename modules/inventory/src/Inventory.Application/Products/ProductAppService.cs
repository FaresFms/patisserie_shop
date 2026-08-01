using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Permissions;
using Inventory.Settings;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Settings;

namespace Inventory.Products;

public class ProductAppService : InventoryAppService, IProductAppService
{
    private const int LookupMaxResults = 100;

    private readonly IProductRepository _productRepository;
    private readonly ProductManager _productManager;
    private readonly ISettingProvider _settingProvider;

    public ProductAppService(
        IProductRepository productRepository,
        ProductManager productManager,
        ISettingProvider settingProvider)
    {
        _productRepository = productRepository;
        _productManager = productManager;
        _settingProvider = settingProvider;
    }

    [Authorize(InventoryPermissions.Products.Default)]
    public async Task<ProductDto> GetAsync(Guid id)
    {
        var product = await _productRepository.GetAsync(id);
        var dto = ObjectMapper.Map<AppProduct, ProductDto>(product);
        dto.Currency = await GetShopCurrencyAsync();
        return dto;
    }

    [Authorize(InventoryPermissions.Products.Default)]
    public async Task<PagedResultDto<ProductDto>> GetListAsync(GetProductsInput input)
    {
        var totalCount = await _productRepository.CountFilteredAsync(
            input.Filter, input.CategoryId, input.DefaultSupplierId, input.IsActive);

        var items = await _productRepository.GetFilteredListAsync(
            input.Filter,
            input.CategoryId,
            input.DefaultSupplierId,
            input.IsActive,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        var currency = await GetShopCurrencyAsync();
        var dtos = items.ConvertAll(p =>
        {
            var dto = ObjectMapper.Map<AppProduct, ProductDto>(p);
            dto.Currency = currency;
            return dto;
        });

        return new PagedResultDto<ProductDto>(totalCount, dtos);
    }

    // Product identity is shared lookup data used by permission-scoped screens
    // outside Inventory (for example Decision Log). Mutating and full-list
    // operations remain protected by their Inventory permissions.
    [Authorize]
    public async Task<List<ProductLookupDto>> GetLookupAsync(string? filter = null)
    {
        var items = await _productRepository.GetActiveLookupAsync(filter, LookupMaxResults);
        var currency = await GetShopCurrencyAsync();
        return items.ConvertAll(p =>
        {
            var dto = ObjectMapper.Map<AppProduct, ProductLookupDto>(p);
            dto.Currency = currency;
            return dto;
        });
    }

    [Authorize(InventoryPermissions.Products.Manage)]
    public async Task<ProductDto> CreateAsync(CreateProductDto input)
    {
        var currency = await GetShopCurrencyAsync();
        var product = await _productManager.CreateAsync(
            input.CategoryId,
            input.Name,
            input.SKU,
            input.Unit,
            input.DefaultSupplierId,
            input.Description,
            input.CostPrice,
            input.SalePrice,
            currency,
            input.ReorderLevel,
            input.ImageUrl,
            input.IsActive,
            input.ShelfLifeDays,
            input.ProductType,
            input.IsSellable,
            input.IsPurchasable,
            input.IsProducible);

        await _productRepository.InsertAsync(product, autoSave: true);
        var dto = ObjectMapper.Map<AppProduct, ProductDto>(product);
        dto.Currency = currency;
        return dto;
    }

    [Authorize(InventoryPermissions.Products.Manage)]
    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductDto input)
    {
        var product = await _productRepository.GetAsync(id);
        var currency = await GetShopCurrencyAsync();

        await _productManager.EnsureReferencesAsync(input.CategoryId, input.DefaultSupplierId);
        await _productManager.ChangeSkuAsync(product, input.SKU);

        product.UpdateInfo(
            input.CategoryId,
            input.DefaultSupplierId,
            input.Name,
            input.Unit,
            input.Description,
            input.CostPrice,
            input.SalePrice,
            currency,
            input.ReorderLevel,
            input.ImageUrl,
            input.IsActive,
            input.ShelfLifeDays,
            input.ProductType,
            input.IsSellable,
            input.IsPurchasable,
            input.IsProducible);

        await _productRepository.UpdateAsync(product, autoSave: true);
        var dto = ObjectMapper.Map<AppProduct, ProductDto>(product);
        dto.Currency = currency;
        return dto;
    }

    [Authorize(InventoryPermissions.Products.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        await _productRepository.DeleteAsync(id);
    }

    private async Task<string> GetShopCurrencyAsync()
        => ShopCurrencySettings.Normalize(
            await _settingProvider.GetOrNullAsync(ShopCurrencySettings.Name));
}
