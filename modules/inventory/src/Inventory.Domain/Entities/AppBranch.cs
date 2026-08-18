using System;
using System.ComponentModel.DataAnnotations.Schema;
using Inventory.Localization;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

public class AppBranch : FullAuditedAggregateRoot<Guid>
{
    public string NameAr { get; internal set; } = null!;
    public string NameEn { get; internal set; } = null!;
    public string? AddressAr { get; private set; }
    public string? AddressEn { get; private set; }
    [NotMapped] public string DisplayName => LocalizedBusinessText.Select(NameAr, NameEn);
    [NotMapped] public string DisplayAddress => LocalizedBusinessText.Select(AddressAr, AddressEn);
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public Guid? ManagerUserId { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// What this branch is for: <see cref="BranchTypes.SalesBranch"/> (sells finished goods)
    /// or <see cref="BranchTypes.MainKitchen"/> (produces goods from raw materials).
    /// </summary>
    public string BranchType { get; private set; } = BranchTypes.SalesBranch;

    protected AppBranch() { }

    internal AppBranch(
        Guid id,
        string nameAr,
        string nameEn,
        string? addressAr = null,
        string? addressEn = null,
        string? phone = null,
        string? email = null,
        Guid? managerUserId = null,
        bool isActive = true,
        string branchType = BranchTypes.SalesBranch)
        : base(id)
    {
        SetNames(nameAr, nameEn);
        AddressAr = NormalizeOptional(addressAr);
        AddressEn = NormalizeOptional(addressEn);
        Phone = phone;
        Email = email;
        ManagerUserId = managerUserId;
        IsActive = isActive;
        SetBranchType(branchType);
    }

    public void UpdateInfo(
        string? addressAr,
        string? addressEn,
        string? phone,
        string? email,
        Guid? managerUserId,
        bool isActive,
        string branchType = BranchTypes.SalesBranch)
    {
        AddressAr = NormalizeOptional(addressAr);
        AddressEn = NormalizeOptional(addressEn);
        Phone = phone;
        Email = email;
        ManagerUserId = managerUserId;
        IsActive = isActive;
        SetBranchType(branchType);
    }

    public void SetBranchType(string branchType)
    {
        if (!BranchTypes.IsValid(branchType))
        {
            throw new BusinessException(InventoryErrorCodes.InvalidBranchType)
                .WithData("BranchType", branchType);
        }

        BranchType = branchType;
    }

    internal void SetNames(string nameAr, string nameEn)
    {
        NameAr = Check.NotNullOrWhiteSpace(nameAr, nameof(nameAr), maxLength: 128).Trim();
        NameEn = Check.NotNullOrWhiteSpace(nameEn, nameof(nameEn), maxLength: 128).Trim();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
