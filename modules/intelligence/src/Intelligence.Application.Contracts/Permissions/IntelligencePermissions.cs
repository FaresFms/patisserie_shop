using Volo.Abp.Reflection;

namespace Intelligence.Permissions;

public class IntelligencePermissions
{
    public const string GroupName = "Intelligence";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(IntelligencePermissions));
    }
}
