namespace Operations;

public static class CashierShiftStatuses
{
    public const string Open = "Open";
    public const string Closed = "Closed";

    public static readonly string[] All =
    {
        Open, Closed
    };

    public static bool IsValid(string? status) => status != null && System.Array.IndexOf(All, status) >= 0;
}
