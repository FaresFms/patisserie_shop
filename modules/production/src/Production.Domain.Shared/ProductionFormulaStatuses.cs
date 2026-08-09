using System;

namespace Production;

public static class ProductionFormulaStatuses
{
    public const string Draft = "Draft";
    public const string Approved = "Approved";
    public const string Retired = "Retired";

    public static readonly string[] All = [Draft, Approved, Retired];

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status)
        && Array.Exists(All, value => string.Equals(value, status, StringComparison.Ordinal));
}
