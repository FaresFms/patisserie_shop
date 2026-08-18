using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.Entities;
using Microsoft.AspNetCore.Authorization;
using Production.Permissions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace Production.Kitchens;

public class KitchenAccessChecker : ITransientDependency
{
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUser _currentUser;

    public KitchenAccessChecker(
        IRepository<AppBranch, Guid> branchRepository,
        IAuthorizationService authorizationService,
        ICurrentUser currentUser)
    {
        _branchRepository = branchRepository;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    public Task<bool> IsManageAllAsync()
        => _authorizationService.IsGrantedAsync(ProductionPermissions.Kitchens.ManageAll);

    public async Task EnsureAccessAsync(Guid kitchenBranchId)
    {
        var userId = _currentUser.Id;
        var canManageAll = await IsManageAllAsync();
        var hasAccess = await _branchRepository.AnyAsync(branch =>
            branch.Id == kitchenBranchId &&
            branch.IsActive &&
            branch.BranchType == BranchTypes.MainKitchen &&
            (canManageAll || (userId.HasValue && branch.ManagerUserId == userId.Value)));

        if (!hasAccess)
        {
            throw new BusinessException(ProductionErrorCodes.KitchenAccessDenied)
                .WithData("KitchenBranchId", kitchenBranchId);
        }
    }

    public async Task<List<AppBranch>> GetAccessibleKitchensAsync()
    {
        var canManageAll = await IsManageAllAsync();
        var userId = _currentUser.Id;
        if (!canManageAll && !userId.HasValue)
        {
            return new List<AppBranch>();
        }

        var kitchens = await _branchRepository.GetListAsync(branch =>
            branch.IsActive &&
            branch.BranchType == BranchTypes.MainKitchen &&
            (canManageAll || branch.ManagerUserId == userId!.Value));
        kitchens.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCulture));
        return kitchens;
    }

    public async Task<List<Guid>> GetAccessibleKitchenIdsAsync()
        => (await GetAccessibleKitchensAsync()).Select(branch => branch.Id).ToList();
}
