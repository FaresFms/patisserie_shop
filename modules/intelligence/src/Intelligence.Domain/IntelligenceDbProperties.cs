namespace Intelligence;

public static class IntelligenceDbProperties
{
    public static string DbTablePrefix { get; set; } = "Intelligence";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "Intelligence";
}
