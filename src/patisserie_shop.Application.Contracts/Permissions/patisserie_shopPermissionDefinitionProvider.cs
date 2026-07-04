using patisserie_shop.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace patisserie_shop.Permissions;

public class patisserie_shopPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(patisserie_shopPermissions.GroupName, L("Permission:ShopGroup"));

        var settings = myGroup.AddPermission(patisserie_shopPermissions.Settings.Default, L("Permission:Settings"));
        settings.AddChild(patisserie_shopPermissions.Settings.Manage, L("Permission:Settings.Manage"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<patisserie_shopResource>(name);
    }
}
