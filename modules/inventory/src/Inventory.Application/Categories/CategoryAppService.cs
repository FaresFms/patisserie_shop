using System;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Inventory.Categories;

[Authorize(InventoryPermissions.Categories.Default)]
public class CategoryAppService : InventoryAppService, ICategoryAppService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly CategoryManager _categoryManager;

    public CategoryAppService(
        ICategoryRepository categoryRepository,
        CategoryManager categoryManager)
    {
        _categoryRepository = categoryRepository;
        _categoryManager = categoryManager;
    }

    public async Task<CategoryDto> GetAsync(Guid id)
    {
        var category = await _categoryRepository.GetAsync(id);
        return ObjectMapper.Map<AppCategory, CategoryDto>(category);
    }

    public async Task<PagedResultDto<CategoryDto>> GetListAsync(GetCategoriesInput input)
    {
        var totalCount = await _categoryRepository.CountFilteredAsync(input.Filter, input.IsActive);

        var items = await _categoryRepository.GetFilteredListAsync(
            input.Filter,
            input.IsActive,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        return new PagedResultDto<CategoryDto>(
            totalCount,
            [.. items.ConvertAll(c => ObjectMapper.Map<AppCategory, CategoryDto>(c))]);
    }

    [Authorize(InventoryPermissions.Categories.Manage)]
    public async Task<CategoryDto> CreateAsync(CreateCategoryDto input)

    {
        var category = await _categoryManager.CreateAsync(input.Name, input.Description, input.IsActive);

        await _categoryRepository.InsertAsync(category, autoSave: true);
        return ObjectMapper.Map<AppCategory, CategoryDto>(category);
    }

    [Authorize(InventoryPermissions.Categories.Manage)]
    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryDto input)
    {
        var category = await _categoryRepository.GetAsync(id);

        await _categoryManager.ChangeNameAsync(category, input.Name);
        category.UpdateInfo(input.Description, input.IsActive);

        await _categoryRepository.UpdateAsync(category, autoSave: true);
        return ObjectMapper.Map<AppCategory, CategoryDto>(category);
    }

    [Authorize(InventoryPermissions.Categories.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        await _categoryRepository.DeleteAsync(id);
    }
}
