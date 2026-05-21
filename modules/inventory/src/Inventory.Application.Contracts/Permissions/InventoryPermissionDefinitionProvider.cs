using Inventory.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Inventory.Permissions;

public class InventoryPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(InventoryPermissions.GroupName, L("Permission:Inventory"));

        var categories = group.AddPermission(InventoryPermissions.Categories.Default, L("Permission:Categories"));
        categories.AddChild(InventoryPermissions.Categories.Manage, L("Permission:Categories.Manage"));

        var suppliers = group.AddPermission(InventoryPermissions.Suppliers.Default, L("Permission:Suppliers"));
        suppliers.AddChild(InventoryPermissions.Suppliers.Manage, L("Permission:Suppliers.Manage"));

        var products = group.AddPermission(InventoryPermissions.Products.Default, L("Permission:Products"));
        products.AddChild(InventoryPermissions.Products.Manage, L("Permission:Products.Manage"));

        var branches = group.AddPermission(InventoryPermissions.Branches.Default, L("Permission:Branches"));
        branches.AddChild(InventoryPermissions.Branches.Manage, L("Permission:Branches.Manage"));

        var branchInventory = group.AddPermission(InventoryPermissions.BranchInventory.Default, L("Permission:BranchInventory"));
        branchInventory.AddChild(InventoryPermissions.BranchInventory.Adjust, L("Permission:BranchInventory.Adjust"));
        branchInventory.AddChild(InventoryPermissions.BranchInventory.Initialize, L("Permission:BranchInventory.Initialize"));
        branchInventory.AddChild(InventoryPermissions.BranchInventory.SetLimits, L("Permission:BranchInventory.SetLimits"));
        branchInventory.AddChild(InventoryPermissions.BranchInventory.ManageAll, L("Permission:BranchInventory.ManageAll"));

        var stockMovements = group.AddPermission(InventoryPermissions.StockMovements.Default, L("Permission:StockMovements"));
        stockMovements.AddChild(InventoryPermissions.StockMovements.ViewAll, L("Permission:StockMovements.ViewAll"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<InventoryResource>(name);
    }
}
