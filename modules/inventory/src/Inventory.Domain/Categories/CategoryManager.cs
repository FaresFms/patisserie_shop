using System;
using System.Threading.Tasks;
using Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Inventory.Categories;

public class CategoryManager : DomainService
{
    private readonly IRepository<AppCategory, Guid> _categoryRepository;

    public CategoryManager(IRepository<AppCategory, Guid> categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<AppCategory> CreateAsync(string name, string? description = null, bool isActive = true)
    {
        await EnsureNameIsUniqueAsync(name);

        return new AppCategory(
            GuidGenerator.Create(),
            name,
            description,
            isActive);
    }

    public async Task ChangeNameAsync(AppCategory category, string newName)
    {
        Check.NotNull(category, nameof(category));
        Check.NotNullOrWhiteSpace(newName, nameof(newName));

        var normalized = newName.Trim();
        if (string.Equals(category.Name, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await EnsureNameIsUniqueAsync(normalized, category.Id);
        category.SetName(normalized);
    }

    private async Task EnsureNameIsUniqueAsync(string name, Guid? ignoreId = null)
    {
        var normalized = name.Trim();
        var exists = await _categoryRepository.AnyAsync(c =>
            c.Name == normalized && (ignoreId == null || c.Id != ignoreId.Value));

        if (exists)
        {
            throw new BusinessException(InventoryErrorCodes.DuplicateCategoryName)
                .WithData("Name", normalized);
        }
    }
}
