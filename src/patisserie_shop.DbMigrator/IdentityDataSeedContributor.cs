using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Intelligence.Permissions;
using Inventory;
using Inventory.Branches;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Operations;
using Operations.Permissions;
using Production.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using AbpIdentityRole = Volo.Abp.Identity.IdentityRole;
using AbpIdentityUser = Volo.Abp.Identity.IdentityUser;

namespace patisserie_shop.DbMigrator;

/// <summary>
/// Seeds the three project-specific roles (Admin, BranchManager, Cashier) with their
/// permission sets and the matching demo users. The ABP-default admin user
/// (admin / 1q2w3E*) is created automatically by ABP's own identity seeder — we only
/// add permissions to the existing admin role here.
///
/// Idempotent: every section is guarded ("if role exists / if user exists")
/// so it's safe to re-run with the DbMigrator.
/// </summary>
public class IdentityDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string AdminRoleName = "admin";
    public const string BranchManagerRoleName = "BranchManager";
    public const string CashierRoleName = "Cashier";
    public const string KitchenManagerRoleName = "KitchenManager";

    public const string BranchManagerUserName = "manager.demo";
    public const string BranchManagerEmail = "manager@patisserie.com";
    public const string BranchManagerPassword = "Manager@2026";

    public const string CashierUserName = "cashier.demo";
    public const string CashierEmail = "cashier@patisserie.com";
    public const string CashierPassword = "Cashier@2026";

    public const string KitchenManagerUserName = "kitchen.demo";
    public const string KitchenManagerEmail = "kitchen@patisserie.com";
    public const string KitchenManagerPassword = "Kitchen@2026";

    /// <summary>The branch cashier.demo is assigned to (matches a seeded branch name).</summary>
    public const string CashierAssignedBranchName = "بوتيك الشارع الرئيسي";

    /// <summary>
    /// The Main Kitchen (production site) branch name. Resolved/created idempotently by
    /// this name; kitchen.demo is set as its ManagerUserId so the existing branch-manager
    /// scoping mechanism scopes the kitchen manager to it.
    /// </summary>
    public const string MainKitchenBranchName = "Main Kitchen";

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
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly BranchManager _branchManager;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly ILogger<IdentityDataSeedContributor> _logger;

    public IdentityDataSeedContributor(
        IIdentityRoleRepository roleRepository,
        IdentityRoleManager roleManager,
        IdentityUserManager userManager,
        IPermissionDataSeeder permissionDataSeeder,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant,
        IRepository<AppBranch, Guid> branchRepository,
        BranchManager branchManager,
        IRepository<AppProduct, Guid> productRepository,
        ILogger<IdentityDataSeedContributor> logger)
    {
        _roleRepository = roleRepository;
        _roleManager = roleManager;
        _userManager = userManager;
        _permissionDataSeeder = permissionDataSeeder;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
        _branchRepository = branchRepository;
        _branchManager = branchManager;
        _productRepository = productRepository;
        _logger = logger;
    }

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        // 1) Roles
        await EnsureRoleExistsAsync(AdminRoleName, isStatic: true);
        await EnsureRoleExistsAsync(BranchManagerRoleName, isStatic: false);
        await EnsureRoleExistsAsync(CashierRoleName, isStatic: false);
        await EnsureRoleExistsAsync(KitchenManagerRoleName, isStatic: false);

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

        await _permissionDataSeeder.SeedAsync(
            RoleProviderName,
            CashierRoleName,
            CashierPermissions(),
            context.TenantId);

        await _permissionDataSeeder.SeedAsync(
            RoleProviderName,
            KitchenManagerRoleName,
            KitchenManagerPermissions(),
            context.TenantId);

        // 3) Demo BranchManager user (only if missing)
        await EnsureUserExistsAsync(
            userName: BranchManagerUserName,
            email: BranchManagerEmail,
            password: BranchManagerPassword,
            roleName: BranchManagerRoleName);

        // 4) Demo Cashier user (only if missing)
        await EnsureUserExistsAsync(
            userName: CashierUserName,
            email: CashierEmail,
            password: CashierPassword,
            roleName: CashierRoleName);

        // 5) Demo KitchenManager user (only if missing) — must exist before the
        // Main Kitchen branch is created so it can be set as the branch's ManagerUserId.
        await EnsureUserExistsAsync(
            userName: KitchenManagerUserName,
            email: KitchenManagerEmail,
            password: KitchenManagerPassword,
            roleName: KitchenManagerRoleName);

        // 6) Assign cashier.demo to the Main Street branch via a persistent claim.
        await EnsureCashierBranchAssignmentAsync();

        // 7) Seed the Main Kitchen (production) branch, managed by kitchen.demo.
        await EnsureMainKitchenBranchAsync();

        // 8) Mark the seeded finished-good (pastry) products as producible so later
        // production waves can build them. Idempotent — only flips IsProducible.
        await EnsureProducibleProductsAsync();
    }

    /// <summary>
    /// Creates the "Main Kitchen" production branch (<see cref="BranchTypes.MainKitchen"/>)
    /// managed by kitchen.demo, idempotently by name. If kitchen.demo can't be resolved yet
    /// it logs and skips; a later migrator run completes it. The branch is created via the
    /// <see cref="BranchManager"/> (same path PatisserieDataSeedContributor uses) so the
    /// name-uniqueness invariant is enforced.
    ///
    /// NOTE: this runs only after at least one branch already exists, so it never trips
    /// PatisserieDataSeedContributor's "branches already exist — skip" count guard on a
    /// fresh database (that seeder seeds its three demo branches first). On a fresh DB where
    /// Identity happens to run before Patisserie, the kitchen branch is deferred to the next
    /// migrator pass — exactly like the cashier branch-assignment above.
    /// </summary>
    private async Task EnsureMainKitchenBranchAsync()
    {
        // Already present (by name)? Idempotent no-op.
        if (await _branchRepository.AnyAsync(b => b.Name == MainKitchenBranchName))
        {
            return;
        }

        // Defer until PatisserieDataSeedContributor has seeded its demo branches, so we
        // never pre-empt its count-based guard and leave the retail branches unseeded.
        if (await _branchRepository.CountAsync() == 0)
        {
            _logger.LogWarning(
                "[Seed] No branches exist yet — deferring Main Kitchen branch creation to the " +
                "next migrator run (so the demo branch seeder runs first).");
            return;
        }

        var kitchenUser = await _userManager.FindByNameAsync(KitchenManagerUserName);
        if (kitchenUser == null)
        {
            _logger.LogWarning(
                "[Seed] Kitchen manager '{User}' not found — skipping Main Kitchen branch creation.",
                KitchenManagerUserName);
            return;
        }

        var branch = await _branchManager.CreateAsync(
            name: MainKitchenBranchName,
            address: null,
            phone: null,
            email: null,
            managerUserId: kitchenUser.Id,
            isActive: true,
            branchType: BranchTypes.MainKitchen);
        await _branchRepository.InsertAsync(branch, autoSave: true);

        _logger.LogInformation(
            "[Seed] Created Main Kitchen branch ({BranchId}) managed by '{User}'.",
            branch.Id, KitchenManagerUserName);
    }

    /// <summary>
    /// Marks the seeded finished-good (pastry) products as producible by flipping
    /// <see cref="AppProduct.IsProducible"/> to true via <c>SetClassification</c>, preserving
    /// the product's existing type / sellable / purchasable flags. Idempotent — only products
    /// that aren't already producible are touched. All currently-seeded active products are
    /// finished goods (pastries); raw-material products and kitchen inventory are deferred to
    /// a later wave.
    /// </summary>
    private async Task EnsureProducibleProductsAsync()
    {
        var products = await _productRepository.GetListAsync(p => p.IsActive && !p.IsProducible);
        if (products.Count == 0)
        {
            return;
        }

        foreach (var product in products)
        {
            product.SetClassification(
                product.ProductType,
                product.IsSellable,
                product.IsPurchasable,
                isProducible: true);
            await _productRepository.UpdateAsync(product, autoSave: true);
        }

        _logger.LogInformation(
            "[Seed] Marked {Count} finished-good product(s) as producible.", products.Count);
    }

    /// <summary>
    /// Assigns cashier.demo to the "Main Street Boutique" branch by writing the persistent
    /// <see cref="CashierClaimTypes.AssignedBranchId"/> claim. Idempotent — if the claim is
    /// already set to that branch nothing changes; otherwise any stale value is replaced.
    /// If the branch can't be resolved yet (e.g. the branch seeder hasn't run on this pass),
    /// it logs and skips gracefully; a subsequent migrator run will complete the assignment.
    /// The claim takes effect on the cashier's NEXT login.
    /// </summary>
    private async Task EnsureCashierBranchAssignmentAsync()
    {
        var cashier = await _userManager.FindByNameAsync(CashierUserName);
        if (cashier == null)
        {
            _logger.LogWarning("[Seed] Cashier '{User}' not found — skipping branch assignment.", CashierUserName);
            return;
        }

        var branches = await _branchRepository.GetListAsync(b => b.Name == CashierAssignedBranchName);
        var branch = branches.FirstOrDefault();
        if (branch == null)
        {
            _logger.LogWarning(
                "[Seed] Branch '{Branch}' not found yet — skipping cashier branch assignment. " +
                "Re-run the migrator after branches are seeded to complete it.",
                CashierAssignedBranchName);
            return;
        }

        var desiredValue = branch.Id.ToString();

        // Read claims through the manager — FindByNameAsync does not populate the Claims
        // navigation collection, so user.Claims would be null here.
        var existing = (await _userManager.GetClaimsAsync(cashier))
            .Where(c => c.Type == CashierClaimTypes.AssignedBranchId)
            .ToList();

        // Already assigned to the right branch? Idempotent no-op.
        if (existing.Count == 1 && existing[0].Value == desiredValue)
        {
            return;
        }

        // Replace any stale/duplicate AssignedBranchId claims with the correct one.
        if (existing.Count > 0)
        {
            await _userManager.RemoveClaimsAsync(cashier, existing);
        }

        await _userManager.AddClaimAsync(
            cashier, new Claim(CashierClaimTypes.AssignedBranchId, desiredValue));

        _logger.LogInformation(
            "[Seed] Assigned cashier '{User}' to branch '{Branch}' ({BranchId}).",
            CashierUserName, branch.Name, branch.Id);
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
        permissions.AddRange(ProductionPermissions.GetAll());

        // Strip the group-name root entries ABP's reflection walk picks up —
        // they aren't real permissions (just the group key).
        return permissions
            .Where(p => p != InventoryPermissions.GroupName
                     && p != OperationsPermissions.GroupName
                     && p != IntelligencePermissions.GroupName
                     && p != ProductionPermissions.GroupName)
            .Distinct();
    }

    /// <summary>
    /// BranchManager: read-only across the catalogue; can adjust their branch's
    /// stock; can receive POs; can manage sales; can create + ship transfers;
    /// can view + acknowledge decision logs; can view all cashier shifts and
    /// create / assign cashiers for the branches they manage (branch-scoped in
    /// the host CashierAssignmentAppService). Cannot manage the catalogue,
    /// cannot approve POs, cannot delete sales, cannot approve transfers
    /// (can request, ship, receive and withdraw their own branch's requests),
    /// cannot see the rules engine.
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

        // Operations — read POs + receive; manage sales; request/ship/receive transfers
        OperationsPermissions.PurchaseOrders.Default,
        OperationsPermissions.PurchaseOrders.Receive,
        OperationsPermissions.Sales.Default,
        OperationsPermissions.Sales.Manage,
        OperationsPermissions.Transfers.Default,
        OperationsPermissions.Transfers.Create,
        OperationsPermissions.Transfers.Ship,
        OperationsPermissions.Transfers.Complete,
        // Cancel is branch-scoped server-side: a manager can only withdraw
        // requests destined to a branch they manage.
        OperationsPermissions.Transfers.Cancel,

        // Operations — cashier oversight: view all shifts + create/assign cashiers
        // (host CashierAssignmentAppService scopes both to the branches they manage)
        OperationsPermissions.Cashier.Default,
        OperationsPermissions.Cashier.ViewAllShifts,
        OperationsPermissions.Cashier.ManageCashiers,

        // Intelligence — decision logs only (no Rules read/manage)
        IntelligencePermissions.DecisionLogs.Default,
        IntelligencePermissions.DecisionLogs.Acknowledge,

        // Production — branch-scoped kitchen requests only.
        ProductionPermissions.BranchRequests.Default
    };

    /// <summary>
    /// Cashier: POS only. Can sell, manage their own shift, void their own sales within
    /// the window, and raise manual low-stock reports. No catalogue, no inventory, no
    /// rules, no manager drawer view (Cashier.ViewAllShifts is intentionally withheld).
    /// </summary>
    private static IEnumerable<string> CashierPermissions() => new[]
    {
        OperationsPermissions.Cashier.Default,
        OperationsPermissions.Cashier.ReportLowStock
    };

    /// <summary>
    /// KitchenManager: runs the Main Kitchen. Gets the full Production permission set
    /// (every node minus the group root) plus a limited slice of the existing catalogue /
    /// operations permissions needed to plan and execute production: read-only catalogue +
    /// branch inventory + stock movements, receive purchase orders for raw materials, and
    /// create / ship stock transfers to dispatch finished goods to sales branches.
    ///
    /// Deliberately withheld: Cashier (POS), Sales.Manage, Products.Manage,
    /// Intelligence Rules, and all Identity admin permissions.
    /// </summary>
    private static IEnumerable<string> KitchenManagerPermissions()
    {
        // All Production permissions except the group-name root entry.
        var permissions = ProductionPermissions.GetAll()
            .Where(p => p != ProductionPermissions.GroupName)
            .ToList();

        // Limited existing permissions the kitchen manager needs (per spec §2).
        permissions.AddRange(new[]
        {
            // Inventory — read-only catalogue + branch stock visibility
            InventoryPermissions.Products.Default,
            InventoryPermissions.Categories.Default,
            InventoryPermissions.Suppliers.Default,
            InventoryPermissions.Branches.Default,
            InventoryPermissions.BranchInventory.Default,
            InventoryPermissions.StockMovements.Default,

            // Operations — receive raw-material POs; create + complete dispatch transfers
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

            // Intelligence — kitchen-scoped decision logs in the page + top notification bell
            IntelligencePermissions.DecisionLogs.Default,
            IntelligencePermissions.DecisionLogs.Acknowledge
        });

        return permissions.Distinct();
    }
}
