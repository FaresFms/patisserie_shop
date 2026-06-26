namespace Production;

public static class ProductionDbProperties
{
    public static string DbTablePrefix { get; set; } = "Production";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "Production";
}
