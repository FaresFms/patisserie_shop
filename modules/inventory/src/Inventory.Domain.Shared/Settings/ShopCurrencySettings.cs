namespace Inventory.Settings;

/// <summary>
/// Single source of truth for the shop-wide currency setting shared by all modules.
/// Entity and DTO defaults remain defensive fallbacks, while application services
/// read this ABP setting whenever they create or project currency-bearing data.
/// </summary>
public static class ShopCurrencySettings
{
    public const string Name = "patisserie_shop.Operations.DefaultCurrency";
    public const string Fallback = "USD";

    public static string Normalize(string? currency)
    {
        currency = currency?.Trim().ToUpperInvariant();
        return currency?.Length == 3 ? currency : Fallback;
    }
}
