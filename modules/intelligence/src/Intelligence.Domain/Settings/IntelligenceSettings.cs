namespace Intelligence.Settings;

public static class IntelligenceSettings
{
    public const string GroupName = "Intelligence";
    public const string BackgroundJobsPrefix = GroupName + ".BackgroundJobs";

    public const string DeadStockScanIntervalMinutes =
        BackgroundJobsPrefix + ".DeadStockScanIntervalMinutes";

    public const string TransferSuggestionScanIntervalMinutes =
        BackgroundJobsPrefix + ".TransferSuggestionScanIntervalMinutes";

    public const string VelocityScanIntervalMinutes =
        BackgroundJobsPrefix + ".VelocityScanIntervalMinutes";

    public const string DecisionOutcomeScanIntervalMinutes =
        BackgroundJobsPrefix + ".DecisionOutcomeScanIntervalMinutes";

    public const string ExpiryScanIntervalMinutes =
        BackgroundJobsPrefix + ".ExpiryScanIntervalMinutes";

    public const int DeadStockDefaultIntervalMinutes = 2;
    public const int TransferSuggestionDefaultIntervalMinutes = 2;
    public const int VelocityDefaultIntervalMinutes = 2;
    public const int DecisionOutcomeDefaultIntervalMinutes = 360;
    public const int ExpiryDefaultIntervalMinutes = 2;

    public const int MinimumIntervalMinutes = 1;
    public const int MaximumIntervalMinutes = 10080; // Seven days.

    public static int NormalizeInterval(int minutes, int defaultValue)
        => minutes < MinimumIntervalMinutes || minutes > MaximumIntervalMinutes
            ? defaultValue
            : minutes;
}
