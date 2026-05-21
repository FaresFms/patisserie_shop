namespace Inventory;

public static class StockMovementTypes
{
    public const string Purchase = "Purchase";
    public const string Sale = "Sale";
    public const string TransferIn = "TransferIn";
    public const string TransferOut = "TransferOut";
    public const string ManualAdjustment = "ManualAdjustment";

    public static readonly string[] All =
    {
        Purchase, Sale, TransferIn, TransferOut, ManualAdjustment
    };

    public static bool IsValid(string? type) => type != null && System.Array.IndexOf(All, type) >= 0;
}
