using Volo.Abp.Reflection;

namespace Operations.Permissions;

public class OperationsPermissions
{
    public const string GroupName = "Operations";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(OperationsPermissions));
    }
}
