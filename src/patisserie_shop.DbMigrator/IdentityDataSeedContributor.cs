using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Permissions;
using Inventory.Permissions;
using Microsoft.AspNetCore.Identity;
using Operations.Permissions;
using patisserie_shop.Permissions;
using Production.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using AbpIdentityRole = Volo.Abp.Identity.IdentityRole;
using AbpIdentityUser = Volo.Abp.Identity.IdentityUser;

namespace patisserie_shop.DbMigrator;

/// <summary>
/// Seeds the project roles, their permissions, and the short presentation accounts.
/// Branch assignment is handled by <see cref="PatisserieDataSeedContributor"/> after the
/// branches exist, so a new database is completely prepared in one migrator run.
/// </summary>
public class IdentityDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string AdminRoleName = "admin";
    public const string BranchManagerRoleName = "BranchManager";
    public const string CashierRoleName = "Cashier";
    public const string KitchenManagerRoleName = "KitchenManager";

    public const string BranchManagerUserName = "manager";
    public const string BranchManagerEmail = "manager@daralward.example";
    public const string BranchManagerPassword = GraduationSeedData.Password;

    public const string CashierUserName = "cashier";
    public const string CashierEmail = "cashier@daralward.example";
    public const string CashierPassword = GraduationSeedData.Password;

    public const string KitchenManagerUserName = "kitchen";
    public const string KitchenManagerEmail = "kitchen@daralward.example";
    public const string KitchenManagerPassword = GraduationSeedData.Password;
    public const string MainKitchenBranchName = GraduationSeedData.MainKitchenName;

    // ABP's RolePermissionValueProvider.ProviderName.
    private const string RoleProviderName = "R";

    private readonly IIdentityRoleRepository _roleRepository;
    private readonly IdentityRoleManager _roleManager;
    private readonly IdentityUserManager _userManager;
    private readonly IPermissionDataSeeder _permissionDataSeeder;
    private readonly IPermissionGrantRepository _permissionGrantRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public IdentityDataSeedContributor(
        IIdentityRoleRepository roleRepository,
        IdentityRoleManager roleManager,
        IdentityUserManager userManager,
        IPermissionDataSeeder permissionDataSeeder,
        IPermissionGrantRepository permissionGrantRepository,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _roleRepository = roleRepository;
        _roleManager = roleManager;
        _userManager = userManager;
        _permissionDataSeeder = permissionDataSeeder;
        _permissionGrantRepository = permissionGrantRepository;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
    }

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        await EnsureRoleExistsAsync(AdminRoleName, isStatic: true);
        await EnsureRoleExistsAsync(BranchManagerRoleName, isStatic: false);
        await EnsureRoleExistsAsync(CashierRoleName, isStatic: false);
        await EnsureRoleExistsAsync(KitchenManagerRoleName, isStatic: false);

        // The built-in ABP admin contributor runs in the same outer seeding unit of
        // work. Flush it first so our permission seeder can see those rows instead
        // of creating duplicate grants for the same role and permission.
        await _unitOfWorkManager.Current!.SaveChangesAsync();

        await _permissionDataSeeder.SeedAsync(
            RoleProviderName, AdminRoleName, AdminPermissions(), context.TenantId);
        await _permissionDataSeeder.SeedAsync(
            RoleProviderName, BranchManagerRoleName, BranchManagerPermissions(), context.TenantId);
        await _permissionDataSeeder.SeedAsync(
            RoleProviderName, CashierRoleName, CashierPermissions(), context.TenantId);
        await _permissionDataSeeder.SeedAsync(
            RoleProviderName, KitchenManagerRoleName, KitchenManagerPermissions(), context.TenantId);
        await _unitOfWorkManager.Current!.SaveChangesAsync();

        // Permission seeding adds missing grants but intentionally does not remove old ones.
        // Reconcile the few role-specific screens that Phase 0 deliberately split.
        await RemoveRolePermissionAsync(AdminRoleName, OperationsPermissions.Cashier.OperatePos);
        await RemoveRolePermissionAsync(AdminRoleName, ProductionPermissions.MyRequests.Default);
        await RemoveRolePermissionAsync(KitchenManagerRoleName, ProductionPermissions.MyRequests.Default);
        await RemoveRolePermissionAsync(KitchenManagerRoleName, ProductionPermissions.Kitchens.ManageAll);
        await RemoveDuplicateRoleGrantsAsync(AdminRoleName);
        await RemoveDuplicateRoleGrantsAsync(BranchManagerRoleName);
        await RemoveDuplicateRoleGrantsAsync(CashierRoleName);
        await RemoveDuplicateRoleGrantsAsync(KitchenManagerRoleName);

        foreach (var user in GraduationSeedData.Users())
        {
            await EnsureUserExistsAsync(user);
        }
    }

    private async Task EnsureRoleExistsAsync(string roleName, bool isStatic)
    {
        var existing = await _roleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
        if (existing != null)
        {
            return;
        }

        var role = new AbpIdentityRole(_guidGenerator.Create(), roleName, _currentTenant.Id)
        {
            IsStatic = isStatic,
            IsPublic = true
        };

        EnsureSucceeded(await _roleManager.CreateAsync(role), $"create role '{roleName}'");
    }

    private async Task RemoveRolePermissionAsync(string roleName, string permissionName)
    {
        var grants = await _permissionGrantRepository.GetListAsync(
            RoleProviderName,
            roleName);
        foreach (var grant in grants.Where(grant => grant.Name == permissionName))
        {
            await _permissionGrantRepository.DeleteAsync(grant);
        }
    }

    private async Task RemoveDuplicateRoleGrantsAsync(string roleName)
    {
        var grants = await _permissionGrantRepository.GetListAsync(RoleProviderName, roleName);
        var duplicates = grants
            .GroupBy(grant => grant.Name, StringComparer.Ordinal)
            .SelectMany(group => group.OrderBy(grant => grant.Id).Skip(1))
            .ToList();

        foreach (var duplicate in duplicates)
        {
            await _permissionGrantRepository.DeleteAsync(duplicate);
        }
    }

    private async Task EnsureUserExistsAsync(UserPresentationSpec spec)
    {
        var existing = await _userManager.FindByNameAsync(spec.UserName)
                       ?? await _userManager.FindByEmailAsync(spec.Email);

        if (existing != null)
        {
            if (!await _userManager.IsInRoleAsync(existing, spec.RoleName))
            {
                EnsureSucceeded(
                    await _userManager.AddToRoleAsync(existing, spec.RoleName),
                    $"add user '{existing.UserName}' to role '{spec.RoleName}'");
            }

            return;
        }

        var user = new AbpIdentityUser(
            _guidGenerator.Create(),
            spec.UserName,
            spec.Email,
            _currentTenant.Id)
        {
            Name = spec.Name,
            Surname = spec.Surname
        };

        EnsureSucceeded(
            await _userManager.CreateAsync(user, GraduationSeedData.Password),
            $"create user '{spec.UserName}'");
        EnsureSucceeded(
            await _userManager.AddToRoleAsync(user, spec.RoleName),
            $"add user '{spec.UserName}' to role '{spec.RoleName}'");
    }

    private static void EnsureSucceeded(IdentityResult result, string what)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(e => e.Description));
        throw new InvalidOperationException($"Failed to {what}: {errors}");
    }

    private static IEnumerable<string> AdminPermissions()
    {
        var permissions = new List<string>();
        permissions.AddRange(InventoryPermissions.GetAll());
        permissions.AddRange(OperationsPermissions.GetAll());
        permissions.AddRange(IntelligencePermissions.GetAll());
        permissions.AddRange(ProductionPermissions.GetAll());
        permissions.Add(patisserie_shopPermissions.Settings.Default);
        permissions.Add(patisserie_shopPermissions.Settings.Manage);

        return permissions
            .Where(p => p != InventoryPermissions.GroupName
                     && p != OperationsPermissions.GroupName
                     && p != OperationsPermissions.Cashier.OperatePos
                     && p != IntelligencePermissions.GroupName
                     && p != ProductionPermissions.GroupName
                     && p != ProductionPermissions.MyRequests.Default)
            .Distinct();
    }

    private static IEnumerable<string> BranchManagerPermissions() =>
    [
        InventoryPermissions.Categories.Default,
        InventoryPermissions.Suppliers.Default,
        InventoryPermissions.Products.Default,
        InventoryPermissions.Branches.Default,
        InventoryPermissions.BranchInventory.Default,
        InventoryPermissions.BranchInventory.Adjust,
        InventoryPermissions.BranchInventory.ReviewStocktakes,
        InventoryPermissions.StockMovements.Default,

        OperationsPermissions.PurchaseOrders.Default,
        OperationsPermissions.PurchaseOrders.Receive,
        OperationsPermissions.Sales.Default,
        OperationsPermissions.Sales.Manage,
        OperationsPermissions.Transfers.Default,
        OperationsPermissions.Transfers.Create,
        OperationsPermissions.Transfers.Ship,
        OperationsPermissions.Transfers.Complete,
        OperationsPermissions.Transfers.Cancel,
        OperationsPermissions.Cashier.Default,
        OperationsPermissions.Cashier.ViewAllShifts,
        OperationsPermissions.Cashier.ManageCashiers,

        IntelligencePermissions.DecisionLogs.Default,
        IntelligencePermissions.DecisionLogs.Acknowledge,
        ProductionPermissions.MyRequests.Default
    ];

    private static IEnumerable<string> CashierPermissions() =>
    [
        OperationsPermissions.Cashier.Default,
        OperationsPermissions.Cashier.OperatePos,
        OperationsPermissions.Cashier.ReportLowStock
    ];

    private static IEnumerable<string> KitchenManagerPermissions()
    {
        var permissions = ProductionPermissions.GetAll()
            .Where(p => p != ProductionPermissions.GroupName
                     && p != ProductionPermissions.MyRequests.Default
                     && p != ProductionPermissions.Kitchens.ManageAll)
            .ToList();

        permissions.AddRange(
        [
            InventoryPermissions.Products.Default,
            InventoryPermissions.Categories.Default,
            InventoryPermissions.Suppliers.Default,
            InventoryPermissions.Branches.Default,
            InventoryPermissions.BranchInventory.Default,
            InventoryPermissions.StockMovements.Default,

            OperationsPermissions.PurchaseOrders.Default,
            OperationsPermissions.PurchaseOrders.Create,
            OperationsPermissions.PurchaseOrders.Edit,
            OperationsPermissions.PurchaseOrders.Receive,
            OperationsPermissions.Transfers.Default,
            OperationsPermissions.Transfers.Create,
            OperationsPermissions.Transfers.ChooseBranches,
            OperationsPermissions.Transfers.Approve,
            OperationsPermissions.Transfers.Ship,
            OperationsPermissions.Transfers.Complete,
            OperationsPermissions.Transfers.Cancel,

            IntelligencePermissions.DecisionLogs.Default,
            IntelligencePermissions.DecisionLogs.Acknowledge
        ]);

        return permissions.Distinct();
    }
}
