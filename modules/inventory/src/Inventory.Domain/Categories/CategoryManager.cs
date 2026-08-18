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

    public async Task<AppCategory> CreateAsync(
        string nameAr,
        string nameEn,
        string? descriptionAr = null,
        string? descriptionEn = null,
        bool isActive = true)
    {
        await EnsureNamesAreUniqueAsync(nameAr, nameEn);

        return new AppCategory(
            GuidGenerator.Create(),
            nameAr,
            nameEn,
            descriptionAr,
            descriptionEn,
            isActive);
    }

    public async Task ChangeNamesAsync(AppCategory category, string nameAr, string nameEn)
    {
        Check.NotNull(category, nameof(category));
        Check.NotNullOrWhiteSpace(nameAr, nameof(nameAr));
        Check.NotNullOrWhiteSpace(nameEn, nameof(nameEn));

        var normalizedAr = nameAr.Trim();
        var normalizedEn = nameEn.Trim();
        if (string.Equals(category.NameAr, normalizedAr, StringComparison.OrdinalIgnoreCase)
            && string.Equals(category.NameEn, normalizedEn, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await EnsureNamesAreUniqueAsync(normalizedAr, normalizedEn, category.Id);
        category.SetNames(normalizedAr, normalizedEn);
    }

    private async Task EnsureNamesAreUniqueAsync(string nameAr, string nameEn, Guid? ignoreId = null)
    {
        var normalizedAr = nameAr.Trim();
        var normalizedEn = nameEn.Trim();
        var exists = await _categoryRepository.AnyAsync(c =>
            (c.NameAr == normalizedAr || c.NameEn == normalizedEn)
            && (ignoreId == null || c.Id != ignoreId.Value));

        if (exists)
        {
            throw new BusinessException(InventoryErrorCodes.DuplicateCategoryName)
                .WithData("NameAr", normalizedAr)
                .WithData("NameEn", normalizedEn);
        }
    }
}
