namespace patisserie_shop.Suppliers;

/// <summary>
/// Deterministic supplier letter-grade thresholds. No AI, no weighting curve — a
/// supplier is graded by the worse of its on-time and fill bands:
///
/// <list type="bullet">
/// <item>A — on-time ≥ 95% AND fill ≥ 98%</item>
/// <item>B — on-time ≥ 85% AND fill ≥ 95%</item>
/// <item>C — on-time ≥ 70% AND fill ≥ 90%</item>
/// <item>D — anything below C</item>
/// </list>
///
/// When either metric is unavailable (no history), the grade is "N/A".
/// </summary>
public static class SupplierGrading
{
    public const string NotAvailable = "N/A";

    public static string Grade(double? onTimeRate, double? fillRate)
    {
        if (onTimeRate is null || fillRate is null)
        {
            return NotAvailable;
        }

        var onTime = onTimeRate.Value;
        var fill = fillRate.Value;

        if (onTime >= 95 && fill >= 98) return "A";
        if (onTime >= 85 && fill >= 95) return "B";
        if (onTime >= 70 && fill >= 90) return "C";
        return "D";
    }
}
