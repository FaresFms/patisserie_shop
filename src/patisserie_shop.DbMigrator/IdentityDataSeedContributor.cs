using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Permissions;
using Inventory.Permissions;
using Microsoft.AspNetCore.Identity;
using Operations.Permissions;
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
/// Seeds the two project-specific roles (Admin, BranchManager) with their
/// permission sets and an optional BranchManager demo user. The ABP-default
/// admin user (admin / 1q2w3E*) is created automatically by ABP's own
/// identity seeder — we only add permissions to the existing admin role here.
///
/// Idempotent: every section is guarded ("if role exists / if user exists")
/// so it's safe to re-run with the DbMigrator.
/// </summary>
public class IdentityDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string AdminRoleName = "admin";
    public const string BranchManagerRoleName = "BranchManager";

    public const string BranchManagerUserName = "manager.demo";
    public const string BranchManagerEmail = "manager@patisserie.com";
    public const string BranchManagerPassword = "Manager@2026";

    // ABP's RolePermissionValueProvider.ProviderName ("R"). Hardcoded here so
    // the seeder doesn't need a transitive dep on the permission-management
    // value-provider type just to read this single constant.
    private const string RoleProviderName = "R";

    private readonly IIdentityRoleRepository _roleRepository;
    private readonly IdentityRoleManager _roleManager;
    private readonly IdentityUserManager _userManager;
    private readonly IPermissionDataSeeder _permissionDataSeeder;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public IdentityDataSeedContributor(
        IIdentityRoleRepository roleRepository,
        IdentityRoleManager roleManager,
        IdentityUserManager userManager,
        IPermissionDataSeeder permissionDataSeeder,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
    {
        _roleRepository = roleRepository;
        _roleManager = roleManager;
        _userManager = userManager;
        _permissionDataSeeder = permissionDataSeeder;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
    }

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        // 1) Roles
        await EnsureRoleExistsAsync(AdminRoleName, isStatic: true);
        await EnsureRoleExistsAsync(BranchManagerRoleName, isStatic: false);

        // 2) Permissions on each role
        await _permissionDataSeeder.SeedAsync(
            RoleProviderName,
            AdminRoleName,
            AdminPermissions(),
            context.TenantId);

        await _permissionDataSeeder.SeedAsync(
            RoleProviderName,
            BranchManagerRoleName,
            BranchManagerPermissions(),
            context.TenantId);

        // 3) Demo BranchManager user (only if missing)
        await EnsureUserExistsAsync(
            userName: BranchManagerUserName,
            email: BranchManagerEmail,
            password: BranchManagerPassword,
            roleName: BranchManagerRoleName);
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

    private async Task EnsureUserExistsAsync(string userName, string email, string password, string roleName)
    {
        // Look up by both keys — a previous seed run may have left a row with
        // either the same username or the same email under a different shape.
        var existing = await _userManager.FindByNameAsync(userName)
                       ?? await _userManager.FindByEmailAsync(email);

        if (existing != null)
        {
            // Already present in some form — just make sure the role is attached.
            if (!await _userManager.IsInRoleAsync(existing, roleName))
            {
                EnsureSucceeded(
                    await _userManager.AddToRoleAsync(existing, roleName),
                    $"add user '{existing.UserName}' to role '{roleName}'");
            }
            return;
        }

        var user = new AbpIdentityUser(_guidGenerator.Create(), userName, email, _currentTenant.Id);

        EnsureSucceeded(await _userManager.CreateAsync(user, password), $"create user '{userName}'");
        EnsureSucceeded(await _userManager.AddToRoleAsync(user, roleName), $"add user '{userName}' to role '{roleName}'");
    }

    private static void EnsureSucceeded(IdentityResult result, string what)
    {
        if (result.Succeeded) return;

        var errors = string.Join("; ", result.Errors.Select(e => e.Description));
        throw new InvalidOperationException($"Failed to {what}: {errors}");
    }

    /// <summary>
    /// Admin gets every defined permission across all three modules.
    /// Built from the reflection helpers so any newly-added permission
    /// constant is granted automatically on the next run.
    /// </summary>
    private static IEnumerable<string> AdminPermissions()
    {
        var permissions = new List<string>();
        permissions.AddRange(InventoryPermissions.GetAll());
        permissions.AddRange(OperationsPermissions.GetAll());
        permissions.AddRange(IntelligencePermissions.GetAll());

        // Strip the group-name root entries ABP's reflection walk picks up —
        // they aren't real permissions (just the group key).
        return permissions
            .Where(p => p != InventoryPermissions.GroupName
                     && p != OperationsPermissions.GroupName
                     && p != IntelligencePermissions.GroupName)
            .Distinct();
    }

    /// <summary>
    /// BranchManager: read-only across the catalogue; can adjust their branch's
    /// stock; can receive POs; can manage sales; can create + ship transfers;
    /// can view + acknowledge decision logs. Cannot manage the catalogue,
    /// cannot approve POs, cannot delete sales, cannot
    /// approve/complete/cancel transfers, cannot see the rules engine.
    ///
    /// NOTE: The spec mentioned "Sales.Confirm" and a "PurchaseOrders.Manage"
    /// umbrella — those exact constants don't exist on OperationsPermissions.
    /// PurchaseOrders has granular Create/Edit/Submit/Approve/Cancel/Receive/Delete
    /// (Admin gets all); Sales.Manage covers the create/confirm workflow.
    /// The mapping below reflects what actually exists in
    /// OperationsPermissions.cs.
    /// </summary>
    private static IEnumerable<string> BranchManagerPermissions() => new[]
    {
        // Inventory — read-only catalogue + branch stock adjustments
        InventoryPermissions.Categories.Default,
        InventoryPermissions.Suppliers.Default,
        InventoryPermissions.Products.Default,
        InventoryPermissions.Branches.Default,
        InventoryPermissions.BranchInventory.Default,
        InventoryPermissions.BranchInventory.Adjust,
        InventoryPermissions.StockMovements.Default,

        // Operations — read POs + receive; manage sales; create/ship transfers
        OperationsPermissions.PurchaseOrders.Default,
        OperationsPermissions.PurchaseOrders.Receive,
        OperationsPermissions.Sales.Default,
        OperationsPermissions.Sales.Manage,
        OperationsPermissions.Transfers.Default,
        OperationsPermissions.Transfers.Create,
        OperationsPermissions.Transfers.Ship,

        // Intelligence — decision logs only (no Rules read/manage)
        IntelligencePermissions.DecisionLogs.Default,
        IntelligencePermissions.DecisionLogs.Acknowledge
    };
}
