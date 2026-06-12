using Volo.Abp.Reflection;

namespace Operations.Permissions;

public class OperationsPermissions
{
    public const string GroupName = "Operations";

    public static class PurchaseOrders
    {
        public const string Default = GroupName + ".PurchaseOrders";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Submit = Default + ".Submit";
        public const string Approve = Default + ".Approve";
        public const string Cancel = Default + ".Cancel";
        public const string Receive = Default + ".Receive";
        public const string Delete = Default + ".Delete";
    }

    public static class Sales
    {
        public const string Default = GroupName + ".Sales";
        public const string Manage = Default + ".Manage";
        public const string Delete = Default + ".Delete";
        public const string ManageAll = Default + ".ManageAll";
    }

    public static class Transfers
    {
        public const string Default = GroupName + ".Transfers";
        public const string Create = Default + ".Create";
        public const string Approve = Default + ".Approve";
        public const string Ship = Default + ".Ship";
        public const string Complete = Default + ".Complete";
        public const string Cancel = Default + ".Cancel";
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(OperationsPermissions));
    }
}
