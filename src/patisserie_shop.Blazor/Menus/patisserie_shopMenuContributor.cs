using System.Threading.Tasks;
using Inventory.Localization;
using Inventory.Permissions;
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

        context.Menu.AddItem(inventoryMenu);

        //Administration
        var administration = context.Menu.GetAdministration();
        administration.Order = 6;

        administration.SetSubItemOrder(IdentityMenuNames.GroupName, 2);
        administration.SetSubItemOrder(SettingManagementMenus.GroupName, 3);

        return Task.CompletedTask;
    }
}
