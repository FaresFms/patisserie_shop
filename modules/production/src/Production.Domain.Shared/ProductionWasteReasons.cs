using System;

namespace Production;

public static class ProductionWasteReasons
{
    public const string Burned = "Burned";
    public const string UnderBaked = "UnderBaked";
    public const string OverBaked = "OverBaked";
    public const string ShapeDamaged = "ShapeDamaged";
    public const string Dropped = "Dropped";
    public const string Contaminated = "Contaminated";
    public const string IngredientSpoilage = "IngredientSpoilage";
    public const string PackagingDamage = "PackagingDamage";
    public const string ExpiredBeforeDispatch = "ExpiredBeforeDispatch";
    public const string TestBatch = "TestBatch";
    public const string Other = "Other";

    public static readonly string[] All =
    {
        Burned,
        UnderBaked,
        OverBaked,
        ShapeDamaged,
        Dropped,
        Contaminated,
        IngredientSpoilage,
        PackagingDamage,
        ExpiredBeforeDispatch,
        TestBatch,
        Other
    };

    public static bool IsValid(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) && Array.IndexOf(All, reason) >= 0;
}
