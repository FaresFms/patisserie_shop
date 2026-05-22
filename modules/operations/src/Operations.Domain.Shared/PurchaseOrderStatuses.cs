namespace Operations;

public static class PurchaseOrderStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string PartialReceived = "PartialReceived";
    public const string Received = "Received";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        Draft, Submitted, Approved, PartialReceived, Received, Cancelled
    };

    public static bool IsTerminal(string status) => status == Received || status == Cancelled;
    public static bool IsReceivable(string status) => status == Approved || status == PartialReceived;
}
