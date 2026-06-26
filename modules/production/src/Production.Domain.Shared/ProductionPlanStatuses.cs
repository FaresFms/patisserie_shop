namespace Production;

public static class ProductionPlanStatuses
{
    public const string Draft = "Draft";
    public const string Confirmed = "Confirmed";
    public const string InProgress = "InProgress";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        Draft, Confirmed, InProgress, Closed, Cancelled
    };

    public static bool IsEditable(string status) => status == Draft;
}
