using System.Globalization;
using Volo.Abp.Settings;

namespace Intelligence.Settings;

public class IntelligenceSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(
            DefineInterval(
                IntelligenceSettings.DeadStockScanIntervalMinutes,
                IntelligenceSettings.DeadStockDefaultIntervalMinutes),
            DefineInterval(
                IntelligenceSettings.TransferSuggestionScanIntervalMinutes,
                IntelligenceSettings.TransferSuggestionDefaultIntervalMinutes),
            DefineInterval(
                IntelligenceSettings.VelocityScanIntervalMinutes,
                IntelligenceSettings.VelocityDefaultIntervalMinutes),
            DefineInterval(
                IntelligenceSettings.DecisionOutcomeScanIntervalMinutes,
                IntelligenceSettings.DecisionOutcomeDefaultIntervalMinutes),
            DefineInterval(
                IntelligenceSettings.ExpiryScanIntervalMinutes,
                IntelligenceSettings.ExpiryDefaultIntervalMinutes)
        );
    }

    private static SettingDefinition DefineInterval(string name, int defaultValue)
        => new(
            name,
            defaultValue.ToString(CultureInfo.InvariantCulture),
            isVisibleToClients: false);
}
