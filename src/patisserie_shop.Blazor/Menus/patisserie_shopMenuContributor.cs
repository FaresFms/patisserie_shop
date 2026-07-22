using System.Threading.Tasks;
using Inventory.Localization;
using Inventory.Permissions;
using Intelligence.Localization;
using Intelligence.Permissions;
using Operations.Localization;
using Operations.Permissions;
using Production.Localization;
using Production.Permissions;
using patisserie_shop.Localization;
using patisserie_shop.Permissions;
using patisserie_shop.MultiTenancy;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.UI.Navigation;
using Volo.Abp.SettingManagement.Blazor.Menus;
using Volo.Abp.Identity.Blazor;

namespace patisserie_shop.Blazor.Menus;

/// <summary>
/// Menu layout follows the "daily work vs. setup" split: the four module groups
/// only contain pages someone opens as part of a normal working day, in rough
/// workflow order. Everything configured once and revisited rarely (catalog,
/// branches, recipes, alert rules, cashier admin, shop settings) lives in the
/// collapsed Setup group at the bottom, next to Administration.
/// </summary>
public class patisserie_shopMenuContributor : IMenuContributor
{
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            await ConfigureMainMenuAsync(context);
        }
    }

    private Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        var l = context.GetLocalizer<patisserie_shopResource>();
        var invL = context.GetLocalizer<InventoryResource>();
        var opsL = context.GetLocalizer<OperationsResource>();
        var intelL = context.GetLocalizer<IntelligenceResource>();
        var prodL = context.GetLocalizer<ProductionResource>();

        context.Menu.Items.Insert(
            0,
            new ApplicationMenuItem(
                patisserie_shopMenus.Home,
                l["Menu:Home"],
                "/",
                icon: "fas fa-home",
                order: 1
            )
        );

        // ── Inventory: watch the stock ─────────────────────────────────────
        var inventoryMenu = new ApplicationMenuItem(
            patisserie_shopMenus.Inventory,
            invL["Menu:Inventory"],
            icon: "fas fa-boxes",
            order: 2
        );

        inventoryMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.InventoryDashboard,
            "Dashboard",
            "/inventory/dashboard",
            icon: "fas fa-chart-pie",
            order: 0
        ).RequirePermissions(InventoryPermissions.BranchInventory.Default));

        // One "Stock" entry — the page itself offers Levels / Batches / History tabs.
        inventoryMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.BranchInventory,
            invL["Menu:Stock"],
            "/inventory/branch-inventory",
            icon: "fas fa-warehouse",
            order: 1
        ).RequirePermissions(InventoryPermissions.BranchInventory.Default));

        inventoryMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.Stocktake,
            invL["Menu:Stocktake"],
            "/inventory/stocktake",
            icon: "fas fa-clipboard-check",
            order: 2
        ).RequirePermissions(InventoryPermissions.BranchInventory.Default));

        inventoryMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.StocktakeReconciliation,
            invL["Menu:StocktakeReconciliation"],
            "/inventory/stocktake-reconciliation",
            icon: "fas fa-balance-scale",
            order: 3
        ).RequirePermissions(
            InventoryPermissions.BranchInventory.Adjust,
            OperationsPermissions.Sales.Default));

        inventoryMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.WasteAnalytics,
            invL["Menu:WasteAnalytics"],
            "/inventory/waste-analytics",
            icon: "fas fa-trash-alt",
            order: 4
        ).RequirePermissions(InventoryPermissions.StockMovements.Default));

        context.Menu.AddItem(inventoryMenu);

        // ── Operations: sell, buy, move ────────────────────────────────────
        var operationsMenu = new ApplicationMenuItem(
            patisserie_shopMenus.Operations,
            opsL["Menu:Operations"],
            icon: "fas fa-clipboard-list",
            order: 3
        );
        operationsMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.Cashier,
            l["Menu:Cashier"],
            "/cashier",
            icon: "fas fa-cash-register",
            order: 0
        ).RequirePermissions(OperationsPermissions.Cashier.Default));
        operationsMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.Sales,
            opsL["Menu:Sales"],
            "/operations/sales",
            icon: "fas fa-receipt",
            order: 1
        ).RequirePermissions(OperationsPermissions.Sales.Default));
        operationsMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.PurchaseOrders,
            opsL["Menu:PurchaseOrders"],
            "/operations/purchase-orders",
            icon: "fas fa-file-invoice-dollar",
            order: 2
        ).RequirePermissions(OperationsPermissions.PurchaseOrders.Default));
        operationsMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.StockTransfers,
            opsL["Menu:StockTransfers"],
            "/operations/stock-transfers",
            icon: "fas fa-exchange-alt",
            order: 3
        ).RequirePermissions(OperationsPermissions.Transfers.Default));
        operationsMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.SalesAnalytics,
            l["Menu:SalesAnalytics"],
            "/operations/sales-analytics",
            icon: "fas fa-chart-line",
            order: 4
        ).RequirePermissions(OperationsPermissions.Sales.Default));
        operationsMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.CashierShifts,
            l["Menu:CashierShifts"],
            "/operations/cashier-shifts",
            icon: "fas fa-money-bill-wave",
            order: 5
        ).RequirePermissions(OperationsPermissions.Cashier.ViewAllShifts));
        context.Menu.AddItem(operationsMenu);

        // ── Intelligence: what the system noticed ──────────────────────────
        var intelligenceMenu = new ApplicationMenuItem(
            patisserie_shopMenus.Intelligence,
            intelL["Menu:Intelligence"],
            icon: "fas fa-sliders-h",
            order: 4
        );
        intelligenceMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.DecisionLog,
            intelL["Menu:DecisionLog"],
            "/intelligence/decision-log",
            icon: "fas fa-clipboard-check",
            order: 0
        ).RequirePermissions(IntelligencePermissions.DecisionLogs.Default));
        intelligenceMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductVelocity,
            intelL["Menu:ProductVelocity"],
            "/intelligence/product-velocity",
            icon: "fas fa-tachometer-alt",
            order: 1
        ).RequirePermissions(IntelligencePermissions.DecisionLogs.Default));
        intelligenceMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ReorderCalendar,
            intelL["Menu:ReorderCalendar"],
            "/intelligence/reorder-calendar",
            icon: "fas fa-calendar-alt",
            order: 2
        ).RequirePermissions(IntelligencePermissions.DecisionLogs.Default));
        context.Menu.AddItem(intelligenceMenu);

        // ── Production: the kitchen's day ──────────────────────────────────
        var productionMenu = new ApplicationMenuItem(
            patisserie_shopMenus.Production,
            prodL["Menu:Production"],
            icon: "fas fa-industry",
            order: 5
        );
        productionMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionDashboard,
            prodL["Menu:ProductionDashboard"],
            "/production/dashboard",
            icon: "fas fa-gauge-high",
            order: 0
        ).RequirePermissions(ProductionPermissions.Dashboard.Default));
        productionMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionMyRequests,
            prodL["Menu:MyKitchenRequests"],
            "/production/my-requests",
            icon: "fas fa-clipboard-list",
            order: 1
        ).RequirePermissions(ProductionPermissions.BranchRequests.Default));
        productionMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionBranchRequests,
            prodL["Menu:BranchRequests"],
            "/production/branch-requests",
            icon: "fas fa-inbox",
            order: 2
        ).RequirePermissions(ProductionPermissions.BranchRequests.Approve));
        productionMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionPlans,
            prodL["Menu:ProductionPlans"],
            "/production/plans",
            icon: "fas fa-calendar-check",
            order: 3
        ).RequirePermissions(ProductionPermissions.Plans.Default));
        productionMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionCook,
            prodL["Menu:CookScreen"],
            "/production/cook",
            icon: "fas fa-fire",
            order: 4
        ).RequirePermissions(ProductionPermissions.Orders.Default));
        productionMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionDispatch,
            prodL["Menu:Dispatch"],
            "/production/dispatch",
            icon: "fas fa-shipping-fast",
            order: 5
        ).RequirePermissions(ProductionPermissions.Dispatch.Default));
        productionMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionWaste,
            prodL["Menu:Waste"],
            "/production/waste",
            icon: "fas fa-trash-can",
            order: 6
        ).RequirePermissions(ProductionPermissions.Waste.Default));
        // Production Analytics intentionally lives behind the production
        // dashboard (toolbar link), not in the sidebar.
        context.Menu.AddItem(productionMenu);

        // ── Setup: configure once, revisit rarely ──────────────────────────
        var setupMenu = new ApplicationMenuItem(
            patisserie_shopMenus.Setup,
            l["Menu:Setup"],
            icon: "fas fa-cog",
            order: 6
        );
        setupMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.Products,
            invL["Menu:Products"],
            "/inventory/products",
            icon: "fas fa-cookie-bite",
            order: 0
        ).RequirePermissions(InventoryPermissions.Products.Default));
        setupMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.Categories,
            invL["Menu:Categories"],
            "/inventory/categories",
            icon: "fas fa-tags",
            order: 1
        ).RequirePermissions(InventoryPermissions.Categories.Default));
        setupMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.Suppliers,
            invL["Menu:Suppliers"],
            "/inventory/suppliers",
            icon: "fas fa-truck",
            order: 2
        ).RequirePermissions(InventoryPermissions.Suppliers.Default));
        setupMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.Branches,
            invL["Menu:Branches"],
            "/inventory/branches",
            icon: "fas fa-store",
            order: 3
        ).RequirePermissions(InventoryPermissions.Branches.Default));
        setupMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionFormulas,
            prodL["Menu:Formulas"],
            "/production/formulas",
            icon: "fas fa-flask",
            order: 4
        ).RequirePermissions(ProductionPermissions.Formulas.Default));
        setupMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.InventoryRules,
            intelL["Menu:InventoryRules"],
            "/intelligence/inventory-rules",
            icon: "fas fa-gavel",
            order: 5
        ).RequirePermissions(IntelligencePermissions.Rules.Default));
        setupMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.CashierAssignments,
            l["Menu:CashierAssignments"],
            "/operations/cashier-assignments",
            icon: "fas fa-user-tag",
            order: 6
        ).RequirePermissions(OperationsPermissions.Cashier.ViewAllShifts));
        setupMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.AddCashier,
            l["Menu:AddCashier"],
            "/operations/add-cashier",
            icon: "fas fa-user-plus",
            order: 7
        ).RequirePermissions(OperationsPermissions.Cashier.ManageCashiers));
        setupMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ShopSettings,
            l["Menu:ShopSettings"],
            "/setup/shop-settings",
            icon: "fas fa-sliders-h",
            order: 8
        ).RequirePermissions(patisserie_shopPermissions.Settings.Manage));
        context.Menu.AddItem(setupMenu);

        //Administration
        var administration = context.Menu.GetAdministration();
        administration.Order = 7;

        administration.SetSubItemOrder(IdentityMenuNames.GroupName, 2);
        administration.SetSubItemOrder(SettingManagementMenus.GroupName, 3);

        return Task.CompletedTask;
    }
}
