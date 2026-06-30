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

        inventoryMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.Categories,
            invL["Menu:Categories"],
            "/inventory/categories",
            icon: "fas fa-tags"
        ).RequirePermissions(InventoryPermissions.Categories.Default));

        inventoryMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.Suppliers,
            invL["Menu:Suppliers"],
            "/inventory/suppliers",
            icon: "fas fa-truck"
        ).RequirePermissions(InventoryPermissions.Suppliers.Default));

        inventoryMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.Products,
            invL["Menu:Products"],
            "/inventory/products",
            icon: "fas fa-cookie-bite"
        ).RequirePermissions(InventoryPermissions.Products.Default));

        inventoryMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.Branches,
            invL["Menu:Branches"],
            "/inventory/branches",
            icon: "fas fa-store"
        ).RequirePermissions(InventoryPermissions.Branches.Default));

        inventoryMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.BranchInventory,
            invL["Menu:BranchInventory"],
            "/inventory/branch-inventory",
            icon: "fas fa-warehouse"
        ).RequirePermissions(InventoryPermissions.BranchInventory.Default));

        inventoryMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.StockMovements,
            invL["Menu:StockMovements"],
            "/inventory/stock-movements",
            icon: "fas fa-history"
        ).RequirePermissions(InventoryPermissions.StockMovements.Default));

        inventoryMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.StockBatches,
            invL["Menu:StockBatches"],
            "/inventory/stock-batches",
            icon: "fas fa-hourglass-half"
        ).RequirePermissions(InventoryPermissions.BranchInventory.Default));

        inventoryMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.WasteAnalytics,
            invL["Menu:WasteAnalytics"],
            "/inventory/waste-analytics",
            icon: "fas fa-trash-alt"
        ).RequirePermissions(InventoryPermissions.StockMovements.Default));

        context.Menu.AddItem(inventoryMenu);

        var opsL = context.GetLocalizer<OperationsResource>();
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
            patisserie_shopMenus.PurchaseOrders,
            opsL["Menu:PurchaseOrders"],
            "/operations/purchase-orders",
            icon: "fas fa-file-invoice-dollar"
        ).RequirePermissions(OperationsPermissions.PurchaseOrders.Default));
        operationsMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.Sales,
            opsL["Menu:Sales"],
            "/operations/sales",
            icon: "fas fa-cash-register"
        ).RequirePermissions(OperationsPermissions.Sales.Default));
        operationsMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.StockTransfers,
            opsL["Menu:StockTransfers"],
            "/operations/stock-transfers",
            icon: "fas fa-exchange-alt"
        ).RequirePermissions(OperationsPermissions.Transfers.Default));
        operationsMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.SalesAnalytics,
            l["Menu:SalesAnalytics"],
            "/operations/sales-analytics",
            icon: "fas fa-chart-line"
        ).RequirePermissions(OperationsPermissions.Sales.Default));
        operationsMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.CashierShifts,
            l["Menu:CashierShifts"],
            "/operations/cashier-shifts",
            icon: "fas fa-money-bill-wave"
        ).RequirePermissions(OperationsPermissions.Cashier.ViewAllShifts));
        operationsMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.CashierAssignments,
            l["Menu:CashierAssignments"],
            "/operations/cashier-assignments",
            icon: "fas fa-user-tag"
        ).RequirePermissions(OperationsPermissions.Cashier.ViewAllShifts));
        operationsMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.AddCashier,
            l["Menu:AddCashier"],
            "/operations/add-cashier",
            icon: "fas fa-user-plus"
        ).RequirePermissions(OperationsPermissions.Cashier.ManageCashiers));
        context.Menu.AddItem(operationsMenu);

        var intelL = context.GetLocalizer<IntelligenceResource>();
        var intelligenceMenu = new ApplicationMenuItem(
            patisserie_shopMenus.Intelligence,
            intelL["Menu:Intelligence"],
            icon: "fas fa-sliders-h",
            order: 4
        );
        intelligenceMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.InventoryRules,
            intelL["Menu:InventoryRules"],
            "/intelligence/inventory-rules",
            icon: "fas fa-gavel"
        ).RequirePermissions(IntelligencePermissions.Rules.Default));
        intelligenceMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.DecisionLog,
            intelL["Menu:DecisionLog"],
            "/intelligence/decision-log",
            icon: "fas fa-clipboard-check"
        ).RequirePermissions(IntelligencePermissions.DecisionLogs.Default));
        intelligenceMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductVelocity,
            intelL["Menu:ProductVelocity"],
            "/intelligence/product-velocity",
            icon: "fas fa-tachometer-alt"
        ).RequirePermissions(IntelligencePermissions.DecisionLogs.Default));
        intelligenceMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ReorderCalendar,
            intelL["Menu:ReorderCalendar"],
            "/intelligence/reorder-calendar",
            icon: "fas fa-calendar-alt"
        ).RequirePermissions(IntelligencePermissions.DecisionLogs.Default));
        context.Menu.AddItem(intelligenceMenu);

        var prodL = context.GetLocalizer<ProductionResource>();
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
            icon: "fas fa-clipboard-list"
        ).RequirePermissions(ProductionPermissions.BranchRequests.Default));
        productionMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionBranchRequests,
            prodL["Menu:BranchRequests"],
            "/production/branch-requests",
            icon: "fas fa-inbox"
        ).RequirePermissions(ProductionPermissions.BranchRequests.Approve));
        productionMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionPlans,
            prodL["Menu:ProductionPlans"],
            "/production/plans",
            icon: "fas fa-calendar-check"
        ).RequirePermissions(ProductionPermissions.Plans.Default));
        productionMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionCook,
            prodL["Menu:CookScreen"],
            "/production/cook",
            icon: "fas fa-fire"
        ).RequirePermissions(ProductionPermissions.Orders.Default));
        productionMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionDispatch,
            prodL["Menu:Dispatch"],
            "/production/dispatch",
            icon: "fas fa-shipping-fast"
        ).RequirePermissions(ProductionPermissions.Dispatch.Default));
        productionMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionWaste,
            prodL["Menu:Waste"],
            "/production/waste",
            icon: "fas fa-trash-can"
        ).RequirePermissions(ProductionPermissions.Waste.Default));
        productionMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionAnalytics,
            prodL["Menu:ProductionAnalytics"],
            "/production/analytics",
            icon: "fas fa-chart-line"
        ).RequirePermissions(ProductionPermissions.Analytics.Default));
        productionMenu.AddItem(new ApplicationMenuItem(
            patisserie_shopMenus.ProductionFormulas,
            prodL["Menu:Formulas"],
            "/production/formulas",
            icon: "fas fa-flask"
        ).RequirePermissions(ProductionPermissions.Formulas.Default));
        context.Menu.AddItem(productionMenu);

        //Administration
        var administration = context.Menu.GetAdministration();
        administration.Order = 6;

        administration.SetSubItemOrder(IdentityMenuNames.GroupName, 2);
        administration.SetSubItemOrder(SettingManagementMenus.GroupName, 3);

        return Task.CompletedTask;
    }
}
