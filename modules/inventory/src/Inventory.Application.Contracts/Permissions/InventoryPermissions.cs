using Volo.Abp.Reflection;

namespace Inventory.Permissions;

public class InventoryPermissions
{
    public const string GroupName = "Inventory";

    public static class Categories
    {
        public const string Default = GroupName + ".Categories";
        public const string Manage = Default + ".Manage";
    }

    public static class Suppliers
    {
        public const string Default = GroupName + ".Suppliers";
        public const string Manage = Default + ".Manage";
    }

    public static class Products
    {
        public const string Default = GroupName + ".Products";
        public const string Manage = Default + ".Manage";
    }

    public static class Branches
    {
        public const string Default = GroupName + ".Branches";
        public const string Manage = Default + ".Manage";
    }

    public static class BranchInventory
    {
        public const string Default = GroupName + ".BranchInventory";
        public const string Adjust = Default + ".Adjust";
        public const string Initialize = Default + ".Initialize";
        public const string SetLimits = Default + ".SetLimits";
        public const string ManageAll = Default + ".ManageAll";
    }

    public static class StockMovements
    {
        public const string Default = GroupName + ".StockMovements";
        public const string ViewAll = Default + ".ViewAll";
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(InventoryPermissions));
    }
}
