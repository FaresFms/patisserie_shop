namespace Production;

public static class ProductionPriorities
{
    public const string Low = "Low";
    public const string Normal = "Normal";
    public const string Urgent = "Urgent";

    public static readonly string[] All = { Low, Normal, Urgent };

    public static bool IsValid(string? priority) =>
        priority != null && System.Array.IndexOf(All, priority) >= 0;
}
