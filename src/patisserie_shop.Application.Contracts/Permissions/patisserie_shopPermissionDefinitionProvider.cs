using patisserie_shop.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace patisserie_shop.Permissions;

public class patisserie_shopPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(patisserie_shopPermissions.GroupName);

        //Define your own permissions here. Example:
        //myGroup.AddPermission(patisserie_shopPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<patisserie_shopResource>(name);
    }
}
