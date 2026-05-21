namespace Operations;

public static class OperationsDbProperties
{
    public static string DbTablePrefix { get; set; } = "Operations";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "Operations";
}
