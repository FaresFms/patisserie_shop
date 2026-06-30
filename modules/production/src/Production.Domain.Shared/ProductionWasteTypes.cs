using System;

namespace Production;

public static class ProductionWasteTypes
{
    public const string RejectedOutput = "RejectedOutput";
    public const string ExpiredFinishedGood = "ExpiredFinishedGood";
    public const string IngredientSpoilage = "IngredientSpoilage";
    public const string ManualWriteOff = "ManualWriteOff";

    public static readonly string[] All =
    {
        RejectedOutput,
        ExpiredFinishedGood,
        IngredientSpoilage,
        ManualWriteOff
    };

    public static bool IsValid(string? wasteType) =>
        !string.IsNullOrWhiteSpace(wasteType) && Array.IndexOf(All, wasteType) >= 0;
}
