using System;
using System.ComponentModel.DataAnnotations.Schema;
using Inventory.Localization;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

public class AppCategory : FullAuditedAggregateRoot<Guid>
{
    public string NameAr { get; internal set; } = null!;
    public string NameEn { get; internal set; } = null!;
    public string? DescriptionAr { get; private set; }
    public string? DescriptionEn { get; private set; }
    [NotMapped] public string DisplayName => LocalizedBusinessText.Select(NameAr, NameEn);
    [NotMapped] public string DisplayDescription => LocalizedBusinessText.Select(DescriptionAr, DescriptionEn);
    public bool IsActive { get; private set; } = true;

    protected AppCategory() { }

    internal AppCategory(
        Guid id,
        string nameAr,
        string nameEn,
        string? descriptionAr = null,
        string? descriptionEn = null,
        bool isActive = true)
        : base(id)
    {
        SetNames(nameAr, nameEn);
        DescriptionAr = NormalizeOptional(descriptionAr);
        DescriptionEn = NormalizeOptional(descriptionEn);
        IsActive = isActive;
    }

    public void UpdateInfo(string? descriptionAr, string? descriptionEn, bool isActive)
    {
        DescriptionAr = NormalizeOptional(descriptionAr);
        DescriptionEn = NormalizeOptional(descriptionEn);
        IsActive = isActive;
    }

    internal void SetNames(string nameAr, string nameEn)
    {
        NameAr = Check.NotNullOrWhiteSpace(nameAr, nameof(nameAr), maxLength: 128).Trim();
        NameEn = Check.NotNullOrWhiteSpace(nameEn, nameof(nameEn), maxLength: 128).Trim();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
