namespace Production;

public static class ProductionOrderStatuses
{
    public const string Draft = "Draft";
    public const string ReadyToCook = "ReadyToCook";
    public const string WaitingForIngredients = "WaitingForIngredients";
    public const string InProduction = "InProduction";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        Draft, ReadyToCook, WaitingForIngredients, InProduction, Completed, Cancelled
    };

    public static bool CanStart(string status) => status == ReadyToCook || status == WaitingForIngredients;
    public static bool CanCancel(string status) => status == Draft || status == ReadyToCook || status == WaitingForIngredients;
}
