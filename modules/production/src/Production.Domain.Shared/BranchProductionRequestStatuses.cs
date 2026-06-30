namespace Production;

public static class BranchProductionRequestStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string PartiallyPlanned = "PartiallyPlanned";
    public const string Planned = "Planned";
    public const string PartiallyFulfilled = "PartiallyFulfilled";
    public const string Fulfilled = "Fulfilled";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        Draft, Submitted, Approved, PartiallyPlanned, Planned,
        PartiallyFulfilled, Fulfilled, Rejected, Cancelled
    };

    public static bool IsEditable(string status) => status == Draft;
    public static bool IsCancellable(string status) => status == Draft || status == Submitted;
    public static bool IsApprovedDemand(string status) =>
        status == Approved || status == PartiallyPlanned || status == Planned || status == PartiallyFulfilled;
}
