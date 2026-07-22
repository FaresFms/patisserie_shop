namespace Inventory;

public static class StocktakeSessionStatuses
{
    public const string Draft = "Draft";
    public const string PendingReview = "PendingReview";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Draft, PendingReview, Completed, Rejected, Cancelled];

    public static bool IsValid(string? status)
        => status is Draft or PendingReview or Completed or Rejected or Cancelled;

    public static bool IsOpen(string? status)
        => status is Draft or PendingReview;
}
