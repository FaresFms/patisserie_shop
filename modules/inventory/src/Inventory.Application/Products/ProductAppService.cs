using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Inventory.Products;

[Authorize(InventoryPermissions.Products.Default)]
public class ProductAppService : InventoryAppService, IProductAppService
{
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppCategory, Guid> _categoryRepository;
    private readonly IRepository<AppSupplier, Guid> _supplierRepository;
    private readonly ProductManager _productManager;

    public ProductAppService(
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppCategory, Guid> categoryRepository,
        IRepository<AppSupplier, Guid> supplierRepository,
        ProductManager productManager)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _supplierRepository = supplierRepository;
        _productManager = productManager;
    }

    public async Task<ProductDto> GetAsync(Guid id)
    {
        var product = await _productRepository.GetAsync(id);
        return ObjectMapper.Map<AppProduct, ProductDto>(product);
    }

    public async Task<PagedResultDto<ProductDto>> GetListAsync(GetProductsInput input)
    {
        var queryable = await _productRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            queryable = queryable.Where(p =>
                p.Name.ToLower().Contains(filter) ||
                p.SKU.ToLower().Contains(filter));
        }

        if (input.CategoryId.HasValue)
        {
            queryable = queryable.Where(p => p.CategoryId == input.CategoryId.Value);
        }

        if (input.DefaultSupplierId.HasValue)
        {
            queryable = queryable.Where(p => p.DefaultSupplierId == input.DefaultSupplierId.Value);
        }

        if (input.IsActive.HasValue)
        {
            queryable = queryable.Where(p => p.IsActive == input.IsActive.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);

        var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? nameof(AppProduct.Name) : input.Sorting;
        queryable = queryable.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount);

        var items = await AsyncExecuter.ToListAsync(queryable);

        return new PagedResultDto<ProductDto>(
            totalCount,
            items.Select(p => ObjectMapper.Map<AppProduct, ProductDto>(p)).ToList()
        );
    }

    public async Task<List<ProductLookupDto>> GetLookupAsync(string? filter = null)
    {
        var queryable = await _productRepository.GetQueryableAsync();
        queryable = queryable.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            queryable = queryable.Where(p => p.Name.ToLower().Contains(f) || p.SKU.ToLower().Contains(f));
        }

        queryable = queryable.OrderBy(p => p.Name).Take(100);

        var items = await AsyncExecuter.ToListAsync(queryable);
        return items.Select(p => ObjectMapper.Map<AppProduct, ProductLookupDto>(p)).ToList();
    }

    [Authorize(InventoryPermissions.Products.Manage)]
    public async Task<ProductDto> CreateAsync(CreateProductDto input)
    {
        await EnsureCategoryExistsAsync(input.CategoryId);
        await EnsureSupplierExistsAsync(input.DefaultSupplierId);

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
            input.IsActive);

        await _productRepository.InsertAsync(product, autoSave: true);
        return ObjectMapper.Map<AppProduct, ProductDto>(product);
    }

    [Authorize(InventoryPermissions.Products.Manage)]
    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductDto input)
    {
        await EnsureCategoryExistsAsync(input.CategoryId);
        await EnsureSupplierExistsAsync(input.DefaultSupplierId);

        var product = await _productRepository.GetAsync(id);

        await _productManager.ChangeSkuAsync(product, input.SKU);

        product.CategoryId = input.CategoryId;
        product.DefaultSupplierId = input.DefaultSupplierId;
        product.Name = input.Name;
        product.Description = input.Description;
        product.Unit = input.Unit;
        product.CostPrice = input.CostPrice;
        product.SalePrice = input.SalePrice;
        product.Currency = input.Currency;
        product.ReorderLevel = input.ReorderLevel;
        product.ImageUrl = input.ImageUrl;
        product.IsActive = input.IsActive;

        await _productRepository.UpdateAsync(product, autoSave: true);
        return ObjectMapper.Map<AppProduct, ProductDto>(product);
    }

    [Authorize(InventoryPermissions.Products.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        await _productRepository.DeleteAsync(id);
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId)
    {
        await _categoryRepository.GetAsync(categoryId);
    }

    private async Task EnsureSupplierExistsAsync(Guid? supplierId)
    {
        if (supplierId.HasValue)
        {
            await _supplierRepository.GetAsync(supplierId.Value);
        }
    }
}
