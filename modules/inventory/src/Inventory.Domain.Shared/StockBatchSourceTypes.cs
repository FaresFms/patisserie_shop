namespace Inventory;

/// <summary>
/// Where a stock batch came from. Stored flat on AppStockBatch.SourceType.
/// "Seed" is used exclusively by the demo data seeder.
/// </summary>
public static class StockBatchSourceTypes
{
    public const string Purchase = "Purchase";
    public const string TransferIn = "TransferIn";
    public const string Adjustment = "Adjustment";
    public const string ProductionOutput = "ProductionOutput";
    public const string Seed = "Seed";

    public static readonly string[] All =
    {
        Purchase, TransferIn, Adjustment, ProductionOutput, Seed
    };

    public static bool IsValid(string? type) => type != null && System.Array.IndexOf(All, type) >= 0;
}
