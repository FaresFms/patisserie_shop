using Intelligence.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Intelligence.Permissions;

public class IntelligencePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(IntelligencePermissions.GroupName, L("Permission:Intelligence"));

        var rules = myGroup.AddPermission(IntelligencePermissions.Rules.Default, L("Permission:Rules"));
        rules.AddChild(IntelligencePermissions.Rules.Manage, L("Permission:Rules.Manage"));

        var decisionLogs = myGroup.AddPermission(IntelligencePermissions.DecisionLogs.Default, L("Permission:DecisionLogs"));
        decisionLogs.AddChild(IntelligencePermissions.DecisionLogs.Acknowledge, L("Permission:DecisionLogs.Acknowledge"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<IntelligenceResource>(name);
    }
}
