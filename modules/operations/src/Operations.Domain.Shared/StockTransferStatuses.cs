namespace Operations;

public static class StockTransferStatuses
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string InTransit = "InTransit";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string Rejected = "Rejected";

    public static readonly string[] All =
    {
        Draft, Pending, Approved, InTransit, Completed, Cancelled, Rejected
    };

    public static bool IsTerminal(string status)
        => status == Completed || status == Cancelled || status == Rejected;
}
