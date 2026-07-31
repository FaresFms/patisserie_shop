using System;
using System.Globalization;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Microsoft.AspNetCore.Authorization;
using patisserie_shop.Permissions;
using Volo.Abp;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;
using IntelligenceJobSettings = Intelligence.Settings.IntelligenceSettings;

namespace patisserie_shop.Settings;

/// <summary>
/// Reads settings through <see cref="ISettingProvider"/> (respecting the ABP
/// fallback chain: per-tenant/global override → defined default) and writes
/// them to the global store via <see cref="ISettingManager"/>. Kept thin: no
/// business rules beyond normalization (uppercasing the currency, clamping the
/// minimum-stock floor to zero).
/// </summary>
[Authorize]
public class ShopSettingsAppService : patisserie_shopAppService, IShopSettingsAppService
{
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;
    private readonly IntelligenceBackgroundJobScheduleNotifier _scheduleNotifier;

    public ShopSettingsAppService(
        ISettingProvider settingProvider,
        ISettingManager settingManager,
        IntelligenceBackgroundJobScheduleNotifier scheduleNotifier)
    {
        _settingProvider = settingProvider;
        _settingManager = settingManager;
        _scheduleNotifier = scheduleNotifier;
    }

    public async Task<ShopSettingsDto> GetAsync()
    {
        return new ShopSettingsDto
        {
            ShopName = await _settingProvider.GetOrNullAsync(patisserie_shopSettings.ShopName) ?? "Patisserie Shop",
            ShopAddress = await _settingProvider.GetOrNullAsync(patisserie_shopSettings.ShopAddress),
            ShopPhone = await _settingProvider.GetOrNullAsync(patisserie_shopSettings.ShopPhone),
            ReceiptFooterMessage = await _settingProvider.GetOrNullAsync(patisserie_shopSettings.ReceiptFooterMessage),
            DefaultCurrency = await _settingProvider.GetOrNullAsync(patisserie_shopSettings.DefaultCurrency) ?? "USD",
            DefaultMinimumStock = await _settingProvider.GetAsync(patisserie_shopSettings.DefaultMinimumStock, defaultValue: 5),
            AutopilotEnabled = await _settingProvider.GetAsync(patisserie_shopSettings.AutopilotEnabled, defaultValue: false),
            DeadStockScanIntervalMinutes = await _settingProvider.GetAsync<int>(
                IntelligenceJobSettings.DeadStockScanIntervalMinutes),
            TransferSuggestionScanIntervalMinutes = await _settingProvider.GetAsync<int>(
                IntelligenceJobSettings.TransferSuggestionScanIntervalMinutes),
            VelocityScanIntervalMinutes = await _settingProvider.GetAsync<int>(
                IntelligenceJobSettings.VelocityScanIntervalMinutes),
            DecisionOutcomeScanIntervalMinutes = await _settingProvider.GetAsync<int>(
                IntelligenceJobSettings.DecisionOutcomeScanIntervalMinutes),
            ExpiryScanIntervalMinutes = await _settingProvider.GetAsync<int>(
                IntelligenceJobSettings.ExpiryScanIntervalMinutes)
        };
    }

    [Authorize(patisserie_shopPermissions.Settings.Manage)]
    public async Task UpdateAsync(ShopSettingsDto input)
    {
        var name = (input.ShopName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(patisserie_shopDomainErrorCodes.ShopNameRequired);
        }

        var currency = (input.DefaultCurrency ?? "USD").Trim().ToUpperInvariant();
        if (currency.Length != 3)
        {
            throw new BusinessException(patisserie_shopDomainErrorCodes.InvalidCurrencyCode)
                .WithData("Currency", currency);
        }

        var minStock = Math.Max(0, input.DefaultMinimumStock);
        var deadStockInterval = NormalizeInterval(
            input.DeadStockScanIntervalMinutes,
            IntelligenceJobSettings.DeadStockDefaultIntervalMinutes);
        var transferSuggestionInterval = NormalizeInterval(
            input.TransferSuggestionScanIntervalMinutes,
            IntelligenceJobSettings.TransferSuggestionDefaultIntervalMinutes);
        var velocityInterval = NormalizeInterval(
            input.VelocityScanIntervalMinutes,
            IntelligenceJobSettings.VelocityDefaultIntervalMinutes);
        var decisionOutcomeInterval = NormalizeInterval(
            input.DecisionOutcomeScanIntervalMinutes,
            IntelligenceJobSettings.DecisionOutcomeDefaultIntervalMinutes);
        var expiryInterval = NormalizeInterval(
            input.ExpiryScanIntervalMinutes,
            IntelligenceJobSettings.ExpiryDefaultIntervalMinutes);

        await _settingManager.SetGlobalAsync(patisserie_shopSettings.ShopName, name);
        await _settingManager.SetGlobalAsync(patisserie_shopSettings.ShopAddress, (input.ShopAddress ?? string.Empty).Trim());
        await _settingManager.SetGlobalAsync(patisserie_shopSettings.ShopPhone, (input.ShopPhone ?? string.Empty).Trim());
        await _settingManager.SetGlobalAsync(patisserie_shopSettings.ReceiptFooterMessage, (input.ReceiptFooterMessage ?? string.Empty).Trim());
        await _settingManager.SetGlobalAsync(patisserie_shopSettings.DefaultCurrency, currency);
        await _settingManager.SetGlobalAsync(patisserie_shopSettings.DefaultMinimumStock, minStock.ToString(CultureInfo.InvariantCulture));
        await _settingManager.SetGlobalAsync(patisserie_shopSettings.AutopilotEnabled, input.AutopilotEnabled.ToString().ToLowerInvariant());
        await SetIntervalAsync(IntelligenceJobSettings.DeadStockScanIntervalMinutes, deadStockInterval);
        await SetIntervalAsync(IntelligenceJobSettings.TransferSuggestionScanIntervalMinutes, transferSuggestionInterval);
        await SetIntervalAsync(IntelligenceJobSettings.VelocityScanIntervalMinutes, velocityInterval);
        await SetIntervalAsync(IntelligenceJobSettings.DecisionOutcomeScanIntervalMinutes, decisionOutcomeInterval);
        await SetIntervalAsync(IntelligenceJobSettings.ExpiryScanIntervalMinutes, expiryInterval);
    }

    private async Task SetIntervalAsync(string settingName, int minutes)
    {
        await _settingManager.SetGlobalAsync(
            settingName,
            minutes.ToString(CultureInfo.InvariantCulture));
        _scheduleNotifier.Notify(settingName, minutes);
    }

    private static int NormalizeInterval(int minutes, int defaultValue)
        => IntelligenceJobSettings.NormalizeInterval(minutes, defaultValue);
}
