using System.ComponentModel.DataAnnotations;

namespace patisserie_shop.Settings;

/// <summary>
/// Flat carrier for the shop-wide settings shown on the "Shop Settings" page.
/// Read via <see cref="IShopSettingsAppService.GetAsync"/>, written back via
/// <see cref="IShopSettingsAppService.UpdateAsync"/>.
/// </summary>
public class ShopSettingsDto
{
    [Required]
    [StringLength(128)]
    public string ShopName { get; set; } = "Patisserie Shop";

    [StringLength(64)]
    public string? ShopPhone { get; set; }

    [StringLength(256)]
    public string? ReceiptFooterMessage { get; set; }

    /// <summary>ISO 4217 code, e.g. "USD", "SAR", "EUR". Stored uppercased.</summary>
    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string DefaultCurrency { get; set; } = "USD";

    [Range(0, 100000)]
    public int DefaultMinimumStock { get; set; } = 5;

    public bool AutopilotEnabled { get; set; } = false;

    [Range(1, 10080)]
    public int DeadStockScanIntervalMinutes { get; set; } = 2;

    [Range(1, 10080)]
    public int TransferSuggestionScanIntervalMinutes { get; set; } = 2;

    [Range(1, 10080)]
    public int VelocityScanIntervalMinutes { get; set; } = 2;

    [Range(1, 10080)]
    public int DecisionOutcomeScanIntervalMinutes { get; set; } = 360;

    [Range(1, 10080)]
    public int ExpiryScanIntervalMinutes { get; set; } = 2;
}
