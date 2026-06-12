using Volo.Abp.Reflection;

namespace Intelligence.Permissions;

public class IntelligencePermissions
{
    public const string GroupName = "Intelligence";

    public static class Rules
    {
        public const string Default = GroupName + ".Rules";
        public const string Manage = Default + ".Manage";
    }

    public static class DecisionLogs
    {
        public const string Default = GroupName + ".DecisionLogs";
        public const string Acknowledge = Default + ".Acknowledge";
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(IntelligencePermissions));
    }
}
