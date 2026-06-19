using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Inventory.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Operations;
using Operations.Permissions;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace patisserie_shop.Cashier;

/// <summary>
/// Admin/manager service that creates cashiers and ties each to ONE branch by writing a
/// persistent <see cref="CashierClaimTypes.AssignedBranchId"/> claim on the cashier's
/// IdentityUser (no new table or migration). Lives in the host because the Operations
/// module does not reference ABP Identity.
///
/// Authorization is per-method: reads + branch assignment require
/// <see cref="OperationsPermissions.Cashier.ViewAllShifts"/> (the cashier-oversight
/// permission managers and admins already hold); creating a cashier requires the stronger
/// <see cref="OperationsPermissions.Cashier.ManageCashiers"/>.
///
/// Branch scoping: a caller without <see cref="OperationsPermissions.Sales.ManageAll"/>
/// (i.e. a branch manager, not an admin) may only see / assign / create cashiers for the
/// branches they manage (<see cref="AppBranch.ManagerUserId"/> == current user). Cashiers
/// with NO branch claim are listed only for admins — a manager has no "their branch"
/// relationship to an unassigned cashier, so showing those would leak users from other
/// branches.
///
/// A claim change takes effect on the cashier's NEXT login, because the principal and its
/// claims are built at login time.
/// </summary>
[Authorize(OperationsPermissions.Cashier.ViewAllShifts)]
public class CashierAssignmentAppService : patisserie_shopAppService, ICashierAssignmentAppService
{
    /// <summary>
    /// The Cashier role name. Mirrors <c>IdentityDataSeedContributor.CashierRoleName</c>
    /// in the DbMigrator (which the host cannot reference). Keep the two in sync.
    /// </summary>
    private const string CashierRoleName = "Cashier";

    private readonly IdentityUserManager _userManager;
    private readonly IRepository<AppBranch, Guid> _branchRepository;

    public CashierAssignmentAppService(
        IdentityUserManager userManager,
        IRepository<AppBranch, Guid> branchRepository)
    {
        _userManager = userManager;
        _branchRepository = branchRepository;
    }

    public async Task<List<CashierAssignmentDto>> GetCashiersAsync()
    {
        var canManageAll = await AuthorizationService.IsGrantedAsync(OperationsPermissions.Sales.ManageAll);

        var assignable = await GetAssignableBranchesAsync();
        var assignableIds = assignable.Select(b => b.Id).ToHashSet();
        var branchNamesById = assignable.ToDictionary(b => b.Id, b => b.Name);

        var cashiers = await _userManager.GetUsersInRoleAsync(CashierRoleName);

        var result = new List<CashierAssignmentDto>(cashiers.Count);
        foreach (var user in cashiers)
        {
            var assignedBranchId = ReadAssignedBranchId(await _userManager.GetClaimsAsync(user));

            // Scope: admins see everyone; a manager sees only cashiers on a branch they
            // manage. Unassigned cashiers are admin-only (a manager has no claim to them).
            if (!canManageAll)
            {
                if (assignedBranchId is not { } id || !assignableIds.Contains(id))
                {
                    continue;
                }
            }

            result.Add(new CashierAssignmentDto
            {
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                AssignedBranchId = assignedBranchId,
                AssignedBranchName = assignedBranchId is { } bid
                    ? branchNamesById.GetValueOrDefault(bid)
                    : null
            });
        }

        return result
            .OrderBy(c => c.UserName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<CashierBranchOptionDto>> GetBranchOptionsAsync()
    {
        var branches = await GetAssignableBranchesAsync();
        return branches
            .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .Select(b => new CashierBranchOptionDto { Id = b.Id, Name = b.Name })
            .ToList();
    }

    public async Task AssignBranchAsync(AssignCashierBranchInput input)
    {
        // Branch must exist, be active, AND be one the caller may assign.
        var branch = await GetAssignableBranchOrThrowAsync(input.BranchId);

        var user = await _userManager.GetByIdAsync(input.UserId);

        // The target must actually be a cashier.
        if (!await _userManager.IsInRoleAsync(user, CashierRoleName))
        {
            throw new BusinessException(OperationsErrorCodes.BranchNotAssignedToCashier)
                .WithData("UserId", input.UserId);
        }

        await ReplaceAssignedBranchClaimAsync(user, branch.Id);
    }

    [Authorize(OperationsPermissions.Cashier.ManageCashiers)]
    public async Task<Guid> CreateCashierAsync(CreateCashierInput input)
    {
        // Manager may only create cashiers for a branch they manage; admin may use any.
        var branch = await GetAssignableBranchOrThrowAsync(input.BranchId);

        var user = new IdentityUser(GuidGenerator.Create(), input.UserName, input.Email, CurrentTenant.Id)
        {
            Name = input.Name,
            Surname = input.Surname
        };

        if (!string.IsNullOrWhiteSpace(input.PhoneNumber))
        {
            user.SetPhoneNumber(input.PhoneNumber, confirmed: false);
        }

        // CheckErrors() throws an AbpIdentityResultException carrying localized messages,
        // so duplicate username/email and weak-password failures surface as validation.
        (await _userManager.CreateAsync(user, input.Password)).CheckErrors();
        (await _userManager.AddToRoleAsync(user, CashierRoleName)).CheckErrors();
        await _userManager.AddClaimAsync(
            user, new Claim(CashierClaimTypes.AssignedBranchId, branch.Id.ToString()));

        return user.Id;
    }

    /// <summary>
    /// The active branches the current caller may assign cashiers to: all of them for an
    /// admin (<see cref="OperationsPermissions.Sales.ManageAll"/>), otherwise only the
    /// branches the caller manages.
    /// </summary>
    private async Task<List<AppBranch>> GetAssignableBranchesAsync()
    {
        if (await AuthorizationService.IsGrantedAsync(OperationsPermissions.Sales.ManageAll))
        {
            return await _branchRepository.GetListAsync(b => b.IsActive);
        }

        var userId = CurrentUser.GetId();
        return await _branchRepository.GetListAsync(b => b.IsActive && b.ManagerUserId == userId);
    }

    /// <summary>
    /// Resolves the target branch and verifies it is active and one the caller may assign,
    /// throwing <see cref="OperationsErrorCodes.BranchNotManagedByYou"/> otherwise.
    /// </summary>
    private async Task<AppBranch> GetAssignableBranchOrThrowAsync(Guid branchId)
    {
        var assignable = await GetAssignableBranchesAsync();
        var branch = assignable.FirstOrDefault(b => b.Id == branchId);
        if (branch is null)
        {
            throw new BusinessException(OperationsErrorCodes.BranchNotManagedByYou)
                .WithData("BranchId", branchId);
        }
        return branch;
    }

    /// <summary>
    /// Replaces any existing AssignedBranchId claim(s) with the given branch. Read/write
    /// through the manager — GetByIdAsync does not populate the Claims navigation, so
    /// user.Claims would be null.
    /// </summary>
    private async Task ReplaceAssignedBranchClaimAsync(IdentityUser user, Guid branchId)
    {
        var existing = (await _userManager.GetClaimsAsync(user))
            .Where(c => c.Type == CashierClaimTypes.AssignedBranchId)
            .ToList();
        if (existing.Count > 0)
        {
            await _userManager.RemoveClaimsAsync(user, existing);
        }

        await _userManager.AddClaimAsync(
            user, new Claim(CashierClaimTypes.AssignedBranchId, branchId.ToString()));
    }

    private static Guid? ReadAssignedBranchId(IList<Claim> claims)
    {
        var raw = claims
            .FirstOrDefault(c => c.Type == CashierClaimTypes.AssignedBranchId)?
            .Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
