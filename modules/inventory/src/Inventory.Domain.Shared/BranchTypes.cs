namespace Inventory;

public static class BranchTypes
{
    /// <summary>A retail/sales location that sells finished goods to customers.</summary>
    public const string SalesBranch = "SalesBranch";

    /// <summary>A production site that turns raw materials into finished/semi-finished goods.</summary>
    public const string MainKitchen = "MainKitchen";

    public static readonly string[] All =
    {
        SalesBranch, MainKitchen
    };

    public static bool IsValid(string? type) => type != null && System.Array.IndexOf(All, type) >= 0;
}
