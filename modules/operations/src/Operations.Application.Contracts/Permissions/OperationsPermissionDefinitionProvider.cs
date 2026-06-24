using Operations.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Operations.Permissions;

public class OperationsPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(OperationsPermissions.GroupName, L("Permission:Operations"));

        var po = group.AddPermission(OperationsPermissions.PurchaseOrders.Default, L("Permission:PurchaseOrders"));
        po.AddChild(OperationsPermissions.PurchaseOrders.Create, L("Permission:PurchaseOrders.Create"));
        po.AddChild(OperationsPermissions.PurchaseOrders.Edit, L("Permission:PurchaseOrders.Edit"));
        po.AddChild(OperationsPermissions.PurchaseOrders.Submit, L("Permission:PurchaseOrders.Submit"));
        po.AddChild(OperationsPermissions.PurchaseOrders.Approve, L("Permission:PurchaseOrders.Approve"));
        po.AddChild(OperationsPermissions.PurchaseOrders.Cancel, L("Permission:PurchaseOrders.Cancel"));
        po.AddChild(OperationsPermissions.PurchaseOrders.Receive, L("Permission:PurchaseOrders.Receive"));
        po.AddChild(OperationsPermissions.PurchaseOrders.Delete, L("Permission:PurchaseOrders.Delete"));

        var sales = group.AddPermission(OperationsPermissions.Sales.Default, L("Permission:Sales"));
        sales.AddChild(OperationsPermissions.Sales.Manage, L("Permission:Sales.Manage"));
        sales.AddChild(OperationsPermissions.Sales.Delete, L("Permission:Sales.Delete"));
        sales.AddChild(OperationsPermissions.Sales.ManageAll, L("Permission:Sales.ManageAll"));

        var transfers = group.AddPermission(OperationsPermissions.Transfers.Default, L("Permission:Transfers"));
        transfers.AddChild(OperationsPermissions.Transfers.Create, L("Permission:Transfers.Create"));
        transfers.AddChild(OperationsPermissions.Transfers.ChooseBranches, L("Permission:Transfers.ChooseBranches"));
        transfers.AddChild(OperationsPermissions.Transfers.Approve, L("Permission:Transfers.Approve"));
        transfers.AddChild(OperationsPermissions.Transfers.Ship, L("Permission:Transfers.Ship"));
        transfers.AddChild(OperationsPermissions.Transfers.Complete, L("Permission:Transfers.Complete"));
        transfers.AddChild(OperationsPermissions.Transfers.Cancel, L("Permission:Transfers.Cancel"));

        var cashier = group.AddPermission(OperationsPermissions.Cashier.Default, L("Permission:Cashier"));
        cashier.AddChild(OperationsPermissions.Cashier.ViewAllShifts, L("Permission:Cashier.ViewAllShifts"));
        cashier.AddChild(OperationsPermissions.Cashier.ReportLowStock, L("Permission:Cashier.ReportLowStock"));
        cashier.AddChild(OperationsPermissions.Cashier.ManageCashiers, L("Permission:Cashier.ManageCashiers"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<OperationsResource>(name);
    }
}
