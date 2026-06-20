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

    public static class Cashier
    {
        /// <summary>Cashier POS: sell, manage own shift, void own sales within the window.</summary>
        public const string Default = GroupName + ".Cashier";

        /// <summary>Manager drawer view: see all cashier shifts/drawers across branches.</summary>
        public const string ViewAllShifts = Default + ".ViewAllShifts";

        /// <summary>Report low stock from the cashier POS to branch managers.</summary>
        public const string ReportLowStock = Default + ".ReportLowStock";

        /// <summary>Create cashier users and assign them to a branch (branch-scoped for managers).</summary>
        public const string ManageCashiers = Default + ".ManageCashiers";
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(OperationsPermissions));
    }
}
