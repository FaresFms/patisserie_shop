namespace Inventory;

public static class StockMovementTypes
{
    public const string Purchase = "Purchase";
    public const string Sale = "Sale";
    public const string TransferIn = "TransferIn";
    public const string TransferOut = "TransferOut";
    public const string ManualAdjustment = "ManualAdjustment";

    /// <summary>
    /// Waste write-off of expired/spoiled stock. Always a decrement; the FEFO batch
    /// ledger consumes EXPIRED batches first for this type (see StockBatchManager).
    /// </summary>
    public const string WriteOff = "WriteOff";

    /// <summary>
    /// Stock returned to a branch when a sale is voided. Always an increment; restores
    /// the quantity removed by the original Sale movement.
    /// </summary>
    public const string SaleReturn = "SaleReturn";

    public static readonly string[] All =
    {
        Purchase, Sale, TransferIn, TransferOut, ManualAdjustment, WriteOff, SaleReturn
    };

    public static bool IsValid(string? type) => type != null && System.Array.IndexOf(All, type) >= 0;
}
