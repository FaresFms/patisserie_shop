using System;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Localization;
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
        return MapToDto(category);
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
            [.. items.ConvertAll(MapToDto)]);
    }

    [Authorize(InventoryPermissions.Categories.Manage)]
    public async Task<CategoryDto> CreateAsync(CreateCategoryDto input)

    {
        var category = await _categoryManager.CreateAsync(
            input.NameAr,
            input.NameEn,
            input.DescriptionAr,
            input.DescriptionEn,
            input.IsActive);

        await _categoryRepository.InsertAsync(category, autoSave: true);
        return MapToDto(category);
    }

    [Authorize(InventoryPermissions.Categories.Manage)]
    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryDto input)
    {
        var category = await _categoryRepository.GetAsync(id);

        await _categoryManager.ChangeNamesAsync(category, input.NameAr, input.NameEn);
        category.UpdateInfo(input.DescriptionAr, input.DescriptionEn, input.IsActive);

        await _categoryRepository.UpdateAsync(category, autoSave: true);
        return MapToDto(category);
    }

    [Authorize(InventoryPermissions.Categories.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        await _categoryRepository.DeleteAsync(id);
    }

    private CategoryDto MapToDto(AppCategory category)
    {
        var dto = ObjectMapper.Map<AppCategory, CategoryDto>(category);
        dto.Name = LocalizedBusinessText.Select(category.NameAr, category.NameEn);
        dto.Description = LocalizedBusinessText.Select(category.DescriptionAr, category.DescriptionEn);
        return dto;
    }
}
