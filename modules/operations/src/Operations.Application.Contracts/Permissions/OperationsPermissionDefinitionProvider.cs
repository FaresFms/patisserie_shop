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
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<OperationsResource>(name);
    }
}
