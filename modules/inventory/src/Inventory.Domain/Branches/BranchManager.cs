using System;
using System.Threading.Tasks;
using Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Inventory.Branches;

public class BranchManager : DomainService
{
    private readonly IBranchRepository _branchRepository;

    public BranchManager(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository;
    }

    public async Task<AppBranch> CreateAsync(
        string nameAr,
        string nameEn,
        string? addressAr = null,
        string? addressEn = null,
        string? phone = null,
        string? email = null,
        Guid? managerUserId = null,
        bool isActive = true,
        string branchType = BranchTypes.SalesBranch)
    {
        await EnsureNamesAreUniqueAsync(nameAr, nameEn);

        return new AppBranch(
            GuidGenerator.Create(),
            nameAr,
            nameEn,
            addressAr,
            addressEn,
            phone,
            email,
            managerUserId,
            isActive,
            branchType);
    }

    public async Task ChangeNamesAsync(AppBranch branch, string nameAr, string nameEn)
    {
        Check.NotNull(branch, nameof(branch));
        Check.NotNullOrWhiteSpace(nameAr, nameof(nameAr));
        Check.NotNullOrWhiteSpace(nameEn, nameof(nameEn));

        var normalizedAr = nameAr.Trim();
        var normalizedEn = nameEn.Trim();
        if (string.Equals(branch.NameAr, normalizedAr, StringComparison.OrdinalIgnoreCase)
            && string.Equals(branch.NameEn, normalizedEn, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await EnsureNamesAreUniqueAsync(normalizedAr, normalizedEn, branch.Id);
        branch.SetNames(normalizedAr, normalizedEn);
    }

    private async Task EnsureNamesAreUniqueAsync(string nameAr, string nameEn, Guid? ignoreId = null)
    {
        var normalizedAr = nameAr.Trim();
        var normalizedEn = nameEn.Trim();
        var exists = await _branchRepository.AnyAsync(b =>
            (b.NameAr == normalizedAr || b.NameEn == normalizedEn)
            && (ignoreId == null || b.Id != ignoreId.Value));

        if (exists)
        {
            throw new BusinessException(InventoryErrorCodes.DuplicateBranchName)
                .WithData("NameAr", normalizedAr)
                .WithData("NameEn", normalizedEn);
        }
    }
}
