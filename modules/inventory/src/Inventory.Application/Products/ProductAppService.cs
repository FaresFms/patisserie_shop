using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Inventory.Products;

[Authorize(InventoryPermissions.Products.Default)]
public class ProductAppService : InventoryAppService, IProductAppService
{
    private const int LookupMaxResults = 100;

    private readonly IProductRepository _productRepository;
    private readonly ProductManager _productManager;

    public ProductAppService(
        IProductRepository productRepository,
        ProductManager productManager)
    {
        _productRepository = productRepository;
        _productManager = productManager;
    }

    public async Task<ProductDto> GetAsync(Guid id)
    {
        var product = await _productRepository.GetAsync(id);
        return ObjectMapper.Map<AppProduct, ProductDto>(product);
    }

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

        return new PagedResultDto<ProductDto>(
            totalCount,
            [.. items.ConvertAll(p => ObjectMapper.Map<AppProduct, ProductDto>(p))]);
    }

    public async Task<List<ProductLookupDto>> GetLookupAsync(string? filter = null)
    {
        var items = await _productRepository.GetActiveLookupAsync(filter, LookupMaxResults);
        return items.ConvertAll(p => ObjectMapper.Map<AppProduct, ProductLookupDto>(p));
    }

    [Authorize(InventoryPermissions.Products.Manage)]
    public async Task<ProductDto> CreateAsync(CreateProductDto input)
    {
        var product = await _productManager.CreateAsync(
            input.CategoryId,
            input.Name,
            input.SKU,
            input.Unit,
            input.DefaultSupplierId,
            input.Description,
            input.CostPrice,
            input.SalePrice,
            input.Currency,
            input.ReorderLevel,
            input.ImageUrl,
            input.IsActive,
            input.ShelfLifeDays,
            input.ProductType,
            input.IsSellable,
            input.IsPurchasable,
            input.IsProducible);

        await _productRepository.InsertAsync(product, autoSave: true);
        return ObjectMapper.Map<AppProduct, ProductDto>(product);
    }

    [Authorize(InventoryPermissions.Products.Manage)]
    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductDto input)
    {
        var product = await _productRepository.GetAsync(id);

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
            input.Currency,
            input.ReorderLevel,
            input.ImageUrl,
            input.IsActive,
            input.ShelfLifeDays,
            input.ProductType,
            input.IsSellable,
            input.IsPurchasable,
            input.IsProducible);

        await _productRepository.UpdateAsync(product, autoSave: true);
        return ObjectMapper.Map<AppProduct, ProductDto>(product);
    }

    [Authorize(InventoryPermissions.Products.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        await _productRepository.DeleteAsync(id);
    }
}
