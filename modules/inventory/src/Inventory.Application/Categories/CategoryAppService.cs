using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Inventory.Categories;

[Authorize(InventoryPermissions.Categories.Default)]
public class CategoryAppService : InventoryAppService, ICategoryAppService
{
    private readonly IRepository<AppCategory, Guid> _categoryRepository;

    public CategoryAppService(IRepository<AppCategory, Guid> categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<CategoryDto> GetAsync(Guid id)
    {
        var category = await _categoryRepository.GetAsync(id);
        return ObjectMapper.Map<AppCategory, CategoryDto>(category);
    }

    public async Task<PagedResultDto<CategoryDto>> GetListAsync(GetCategoriesInput input)
    {
        var queryable = await _categoryRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            queryable = queryable.Where(c => c.Name.ToLower().Contains(filter));
        }

        if (input.IsActive.HasValue)
        {
            queryable = queryable.Where(c => c.IsActive == input.IsActive.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);

        var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? nameof(AppCategory.Name) : input.Sorting;
        queryable = queryable.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount);

        var items = await AsyncExecuter.ToListAsync(queryable);

        return new PagedResultDto<CategoryDto>(
            totalCount,
            items.Select(c => ObjectMapper.Map<AppCategory, CategoryDto>(c)).ToList()
        );
    }

    [Authorize(InventoryPermissions.Categories.Manage)]
    public async Task<CategoryDto> CreateAsync(CreateCategoryDto input)
    {
        var category = new AppCategory(
            GuidGenerator.Create(),
            input.Name,
            input.Description,
            input.IsActive
        );

        await _categoryRepository.InsertAsync(category, autoSave: true);
        return ObjectMapper.Map<AppCategory, CategoryDto>(category);
    }

    [Authorize(InventoryPermissions.Categories.Manage)]
    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryDto input)
    {
        var category = await _categoryRepository.GetAsync(id);
        category.Name = input.Name;
        category.Description = input.Description;
        category.IsActive = input.IsActive;
        await _categoryRepository.UpdateAsync(category, autoSave: true);
        return ObjectMapper.Map<AppCategory, CategoryDto>(category);
    }

    [Authorize(InventoryPermissions.Categories.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        await _categoryRepository.DeleteAsync(id);
    }
}
