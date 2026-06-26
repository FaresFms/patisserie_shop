using System;
using System.Threading.Tasks;
using Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Inventory.Branches;

public class BranchManager : DomainService
{
    private readonly IRepository<AppBranch, Guid> _branchRepository;

    public BranchManager(IRepository<AppBranch, Guid> branchRepository)
    {
        _branchRepository = branchRepository;
    }

    public async Task<AppBranch> CreateAsync(
        string name,
        string? address = null,
        string? phone = null,
        string? email = null,
        Guid? managerUserId = null,
        bool isActive = true,
        string branchType = BranchTypes.SalesBranch)
    {
        await EnsureNameIsUniqueAsync(name);

        return new AppBranch(
            GuidGenerator.Create(),
            name,
            address,
            phone,
            email,
            managerUserId,
            isActive,
            branchType);
    }

    public async Task ChangeNameAsync(AppBranch branch, string newName)
    {
        Check.NotNull(branch, nameof(branch));
        Check.NotNullOrWhiteSpace(newName, nameof(newName));

        var normalized = newName.Trim();
        if (string.Equals(branch.Name, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await EnsureNameIsUniqueAsync(normalized, branch.Id);
        branch.SetName(normalized);
    }

    private async Task EnsureNameIsUniqueAsync(string name, Guid? ignoreId = null)
    {
        var normalized = name.Trim();
        var exists = await _branchRepository.AnyAsync(b =>
            b.Name == normalized && (ignoreId == null || b.Id != ignoreId.Value));

        if (exists)
        {
            throw new BusinessException(InventoryErrorCodes.DuplicateBranchName)
                .WithData("Name", normalized);
        }
    }
}
