using Volo.Abp.Reflection;

namespace Production.Permissions;

public class ProductionPermissions
{
    public const string GroupName = "Production";

    public static class Dashboard
    {
        public const string Default = GroupName + ".Dashboard";
    }

    public static class Formulas
    {
        public const string Default = GroupName + ".Formulas";
        public const string Manage = Default + ".Manage";
    }

    public static class BranchRequests
    {
        public const string Default = GroupName + ".BranchRequests";
        public const string Approve = Default + ".Approve";
        public const string Reject = Default + ".Reject";
    }

    public static class Plans
    {
        public const string Default = GroupName + ".Plans";
        public const string Manage = Default + ".Manage";
    }

    public static class Orders
    {
        public const string Default = GroupName + ".Orders";
        public const string Create = Default + ".Create";
        public const string Start = Default + ".Start";
        public const string Complete = Default + ".Complete";
        public const string Cancel = Default + ".Cancel";
    }

    public static class Ingredients
    {
        public const string Default = GroupName + ".Ingredients";
        public const string CheckAvailability = Default + ".CheckAvailability";
    }

    public static class Waste
    {
        public const string Default = GroupName + ".Waste";
        public const string WriteOff = Default + ".WriteOff";
    }

    public static class Dispatch
    {
        public const string Default = GroupName + ".Dispatch";
        public const string CreateTransfer = Default + ".CreateTransfer";
        public const string Ship = Default + ".Ship";
    }

    public static class Analytics
    {
        public const string Default = GroupName + ".Analytics";
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(ProductionPermissions));
    }
}
