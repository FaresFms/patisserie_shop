namespace patisserie_shop.Settings;

/// <summary>
/// Setting keys for shop-wide configuration. These back the "Shop Settings"
/// admin page and are read by the cashier receipt, the top-bar branding, the
/// default currency on new documents, and the default minimum-stock level when
/// initializing branch inventory. All have sensible defaults defined in
/// <see cref="patisserie_shopSettingDefinitionProvider"/>, so the system works
/// out of the box and the owner only changes what they care about.
/// </summary>
public static class patisserie_shopSettings
{
    private const string Prefix = "patisserie_shop";

    /// <summary>Group prefix for the shop-identity settings (branding + receipt).</summary>
    public const string ShopPrefix = Prefix + ".Shop";

    /// <summary>Display name of the shop — top bar and receipt header. Default "Patisserie Shop".</summary>
    public const string ShopName = ShopPrefix + ".Name";

    /// <summary>Street address printed on the receipt footer. Default empty.</summary>
    public const string ShopAddress = ShopPrefix + ".Address";

    /// <summary>Phone number printed on the receipt footer. Default empty.</summary>
    public const string ShopPhone = ShopPrefix + ".Phone";

    /// <summary>Thank-you / footer line printed at the bottom of every receipt.</summary>
    public const string ReceiptFooterMessage = ShopPrefix + ".ReceiptFooterMessage";

    /// <summary>Group prefix for operational defaults.</summary>
    public const string OperationsPrefix = Prefix + ".Operations";

    /// <summary>
    /// ISO 4217 currency code used as the default for new products, purchase
    /// orders, and sales when none is supplied. Default "USD".
    /// </summary>
    public const string DefaultCurrency = OperationsPrefix + ".DefaultCurrency";

    /// <summary>
    /// Default minimum-stock level pre-filled when initializing a product at a
    /// branch. Default 5. Purely a UI convenience — the manager can override it
    /// per product.
    /// </summary>
    public const string DefaultMinimumStock = OperationsPrefix + ".DefaultMinimumStock";

    /// <summary>
    /// Whether low-stock alerts may create draft purchase orders automatically
    /// (a shop-wide master switch surfaced in plain words on the settings page).
    /// This does not itself run autopilot — it gates whether the rule-level
    /// ActionMode is honored. Default true.
    /// </summary>
    public const string AutopilotEnabled = OperationsPrefix + ".AutopilotEnabled";
}
