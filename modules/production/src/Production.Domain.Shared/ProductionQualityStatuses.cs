using System;

namespace Production;

public static class ProductionQualityStatuses
{
    public const string NotRequired = "NotRequired";
    public const string Pending = "Pending";
    public const string Held = "Held";
    public const string Released = "Released";
    public const string Rejected = "Rejected";

    public static readonly string[] All = [NotRequired, Pending, Held, Released, Rejected];

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status)
        && Array.Exists(All, value => string.Equals(value, status, StringComparison.Ordinal));
}
