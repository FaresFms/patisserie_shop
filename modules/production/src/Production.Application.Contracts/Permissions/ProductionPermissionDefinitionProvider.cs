using Production.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Production.Permissions;

public class ProductionPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(ProductionPermissions.GroupName, L("Permission:Production"));

        myGroup.AddPermission(ProductionPermissions.Dashboard.Default, L("Permission:Dashboard"));

        var formulas = myGroup.AddPermission(ProductionPermissions.Formulas.Default, L("Permission:Formulas"));
        formulas.AddChild(ProductionPermissions.Formulas.Manage, L("Permission:Formulas.Manage"));

        var branchRequests = myGroup.AddPermission(ProductionPermissions.BranchRequests.Default, L("Permission:BranchRequests"));
        branchRequests.AddChild(ProductionPermissions.BranchRequests.Approve, L("Permission:BranchRequests.Approve"));
        branchRequests.AddChild(ProductionPermissions.BranchRequests.Reject, L("Permission:BranchRequests.Reject"));

        var plans = myGroup.AddPermission(ProductionPermissions.Plans.Default, L("Permission:Plans"));
        plans.AddChild(ProductionPermissions.Plans.Manage, L("Permission:Plans.Manage"));

        var orders = myGroup.AddPermission(ProductionPermissions.Orders.Default, L("Permission:Orders"));
        orders.AddChild(ProductionPermissions.Orders.Create, L("Permission:Orders.Create"));
        orders.AddChild(ProductionPermissions.Orders.Start, L("Permission:Orders.Start"));
        orders.AddChild(ProductionPermissions.Orders.Complete, L("Permission:Orders.Complete"));
        orders.AddChild(ProductionPermissions.Orders.Cancel, L("Permission:Orders.Cancel"));

        var ingredients = myGroup.AddPermission(ProductionPermissions.Ingredients.Default, L("Permission:Ingredients"));
        ingredients.AddChild(ProductionPermissions.Ingredients.CheckAvailability, L("Permission:Ingredients.CheckAvailability"));

        var waste = myGroup.AddPermission(ProductionPermissions.Waste.Default, L("Permission:Waste"));
        waste.AddChild(ProductionPermissions.Waste.WriteOff, L("Permission:Waste.WriteOff"));

        var dispatch = myGroup.AddPermission(ProductionPermissions.Dispatch.Default, L("Permission:Dispatch"));
        dispatch.AddChild(ProductionPermissions.Dispatch.CreateTransfer, L("Permission:Dispatch.CreateTransfer"));
        dispatch.AddChild(ProductionPermissions.Dispatch.Ship, L("Permission:Dispatch.Ship"));

        myGroup.AddPermission(ProductionPermissions.Analytics.Default, L("Permission:Analytics"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<ProductionResource>(name);
    }
}
