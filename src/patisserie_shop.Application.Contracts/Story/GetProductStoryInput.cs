using System;

namespace patisserie_shop.Story;

public class GetProductStoryInput
{
    public Guid ProductId { get; set; }

    public Guid BranchId { get; set; }

    /// <summary>
    /// Maximum number of timeline events returned after the three sources are
    /// merged. Clamped server-side to [1, 200]; non-positive values fall back
    /// to the default of 100.
    /// </summary>
    public int MaxEvents { get; set; } = 100;
}
