using Intelligence.Entities;

namespace Intelligence.Velocity;

/// <summary>
/// Typed read-model returned by <see cref="IProductVelocityRepository"/>:
/// a velocity row overlaid with its product / branch display data and the
/// branch's current stock. Kept flat (no Inventory entity references) so
/// Intelligence.Domain stays free of a dependency on Inventory.Domain.
/// </summary>
public class ProductVelocityListRow
{
    public AppProductVelocity Velocity { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string ProductSku { get; set; } = null!;
    public string BranchName { get; set; } = null!;
    public int CurrentStock { get; set; }

    /// <summary>CurrentStock ÷ AvgDailySales30; null when the 30-day velocity is zero.</summary>
    public decimal? DaysOfCover { get; set; }
}
