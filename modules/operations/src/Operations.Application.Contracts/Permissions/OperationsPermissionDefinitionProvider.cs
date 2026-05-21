using Operations.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Operations.Permissions;

public class OperationsPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(OperationsPermissions.GroupName, L("Permission:Operations"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<OperationsResource>(name);
    }
}
