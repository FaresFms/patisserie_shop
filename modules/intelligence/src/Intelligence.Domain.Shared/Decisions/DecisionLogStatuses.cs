using System;

namespace Intelligence.Decisions;

/// <summary>
/// Status values for AppDecisionLog. A log is born Pending; it can move once to
/// Acknowledged, Dismissed, or Executed via the corresponding entity method.
/// Stored as a flat string on AppDecisionLog.Status.
/// </summary>
public static class DecisionLogStatuses
{
    public const string Pending = "Pending";
    public const string Acknowledged = "Acknowledged";
    public const string Dismissed = "Dismissed";
    public const string Executed = "Executed";

    public static readonly string[] All =
    {
        Pending,
        Acknowledged,
        Dismissed,
        Executed
    };

    public static readonly string[] Resolved =
    {
        Acknowledged,
        Dismissed,
        Executed
    };

    public static bool IsValid(string? status)
        => !string.IsNullOrWhiteSpace(status) && Array.IndexOf(All, status) >= 0;
}
