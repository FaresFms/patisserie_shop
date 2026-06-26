namespace Inventory;

public static class ProductTypes
{
    /// <summary>A sellable end product (e.g. a finished cake).</summary>
    public const string FinishedGood = "FinishedGood";

    /// <summary>A raw ingredient consumed by production (e.g. flour, sugar).</summary>
    public const string RawMaterial = "RawMaterial";

    /// <summary>Packaging material (e.g. boxes, bags).</summary>
    public const string Packaging = "Packaging";

    /// <summary>An intermediate product produced then consumed by further production.</summary>
    public const string SemiFinished = "SemiFinished";

    public static readonly string[] All =
    {
        FinishedGood, RawMaterial, Packaging, SemiFinished
    };

    public static bool IsValid(string? type) => type != null && System.Array.IndexOf(All, type) >= 0;
}
