using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace Inventory.BranchInventory;

public class BranchAccessChecker : ITransientDependency
{
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUser _currentUser;

    public BranchAccessChecker(
        IRepository<AppBranch, Guid> branchRepository,
        IAuthorizationService authorizationService,
        ICurrentUser currentUser)
    {
        _branchRepository = branchRepository;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    public Task<bool> IsManageAllAsync()
        => _authorizationService.IsGrantedAsync(InventoryPermissions.BranchInventory.ManageAll);

    public async Task EnsureAccessAsync(Guid branchId)
    {
        if (await IsManageAllAsync()) return;

        var userId = _currentUser.Id;
        if (userId == null ||
            !await _branchRepository.AnyAsync(b => b.Id == branchId && b.ManagerUserId == userId))
        {
            throw new BusinessException(InventoryErrorCodes.BranchAccessDenied)
                .WithData("BranchId", branchId);
        }
    }

    public async Task<List<Guid>> GetAccessibleBranchIdsAsync()
    {
        if (await IsManageAllAsync())
        {
            var all = await _branchRepository.GetListAsync(b => b.IsActive);
            return all.Select(b => b.Id).ToList();
        }

        var userId = _currentUser.Id;
        if (userId == null) return new List<Guid>();

        var mine = await _branchRepository.GetListAsync(b => b.IsActive && b.ManagerUserId == userId);
        return mine.Select(b => b.Id).ToList();
    }

    public async Task<List<Guid>?> GetScopedBranchIdsAsync(string manageAllPermission)
    {
        if (await _authorizationService.IsGrantedAsync(manageAllPermission))
        {
            return null;
        }

        var userId = _currentUser.Id;
        if (userId == null) return new List<Guid>();

        var mine = await _branchRepository.GetListAsync(b => b.ManagerUserId == userId);
        return mine.Select(b => b.Id).ToList();
    }
}
