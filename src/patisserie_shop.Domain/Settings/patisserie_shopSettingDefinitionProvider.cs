using patisserie_shop.Localization;
using Inventory.Settings;
using Volo.Abp.Localization;
using Volo.Abp.Settings;

namespace patisserie_shop.Settings;

/// <summary>
/// Defines the shop-wide settings with human-friendly display names/descriptions
/// (localized) and sensible defaults. <c>isVisibleToClients: true</c> lets the
/// Blazor client read them; they are edited through the custom "Shop Settings"
/// page rather than the raw ABP setting-management UI.
/// </summary>
public class patisserie_shopSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(
            new SettingDefinition(
                patisserie_shopSettings.ShopName,
                defaultValue: "Patisserie Shop",
                displayName: L("Setting:ShopName"),
                description: L("Setting:ShopName:Desc"),
                isVisibleToClients: true),

            new SettingDefinition(
                patisserie_shopSettings.ShopPhone,
                defaultValue: "",
                displayName: L("Setting:ShopPhone"),
                description: L("Setting:ShopPhone:Desc"),
                isVisibleToClients: true),

            new SettingDefinition(
                patisserie_shopSettings.ReceiptFooterMessage,
                defaultValue: "Thank you for your visit!",
                displayName: L("Setting:ReceiptFooterMessage"),
                description: L("Setting:ReceiptFooterMessage:Desc"),
                isVisibleToClients: true),

            new SettingDefinition(
                patisserie_shopSettings.DefaultCurrency,
                defaultValue: ShopCurrencySettings.Fallback,
                displayName: L("Setting:DefaultCurrency"),
                description: L("Setting:DefaultCurrency:Desc"),
                isVisibleToClients: true),

            new SettingDefinition(
                patisserie_shopSettings.DefaultMinimumStock,
                defaultValue: "5",
                displayName: L("Setting:DefaultMinimumStock"),
                description: L("Setting:DefaultMinimumStock:Desc"),
                isVisibleToClients: true),

            new SettingDefinition(
                patisserie_shopSettings.AutopilotEnabled,
                // Off by default: automation that creates and submits orders as the admin
                // is opt-in. The owner turns it on in Setup → Shop settings after they
                // trust the suggestions.
                defaultValue: "false",
                displayName: L("Setting:AutopilotEnabled"),
                description: L("Setting:AutopilotEnabled:Desc"),
                isVisibleToClients: true),

            new SettingDefinition(
                patisserie_shopSettings.ProductionControlProfile,
                defaultValue: "{\"RequireScheduleBeforeStart\":true,\"RequireOperatorBeforeStart\":true,\"RequireQualityReleaseBeforeDispatch\":true,\"ForecastSafetyPercent\":10,\"ForecastAccuracyWindowDays\":30,\"WorkCenters\":[{\"Code\":\"MAIN\",\"Name\":\"Main production line\",\"CapacityUnitsPerHour\":100,\"ParallelSlots\":1,\"IsActive\":true}],\"Shifts\":[{\"Code\":\"DAY\",\"Name\":\"Day shift\",\"StartTime\":\"06:00:00\",\"EndTime\":\"14:00:00\",\"IsActive\":true}]}",
                displayName: L("Setting:ProductionControlProfile"),
                description: L("Setting:ProductionControlProfile:Desc"),
                isVisibleToClients: false)
        );
    }

    private static LocalizableString L(string name)
        => LocalizableString.Create<patisserie_shopResource>(name);
}
