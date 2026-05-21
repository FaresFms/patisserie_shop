using Intelligence.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Intelligence.Permissions;

public class IntelligencePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(IntelligencePermissions.GroupName, L("Permission:Intelligence"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<IntelligenceResource>(name);
    }
}
