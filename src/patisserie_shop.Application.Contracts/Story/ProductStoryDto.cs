using System;
using System.Collections.Generic;

namespace patisserie_shop.Story;

public class ProductStoryDto
{
    public Guid ProductId { get; set; }
    public Guid BranchId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;

    /// <summary>QuantityOnHand of the AppBranchInventory row; 0 when the product was never initialized at the branch.</summary>
    public int CurrentStock { get; set; }

    /// <summary>Units sold at this branch in the trailing 30 days.</summary>
    public int TotalSold30Days { get; set; }

    /// <summary>All batches ever received for this product at this branch (incl. depleted).</summary>
    public int BatchCount { get; set; }

    /// <summary>Whole days since the last recorded sale; null when never sold.</summary>
    public int? DaysSinceLastSale { get; set; }

    /// <summary>Merged timeline, newest first, capped at the requested MaxEvents.</summary>
    public List<StoryEventDto> Events { get; set; } = new();
}

public class StoryEventDto
{
    public DateTime OccurredAt { get; set; }

    /// <summary>One of <see cref="StoryEventTypes"/>.</summary>
    public string EventType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Detail { get; set; }

    /// <summary>Signed stock change (+20 / −3); null for events that don't move stock (decisions, batches).</summary>
    public int? QuantityDelta { get; set; }
}

/// <summary>
/// Event-type discriminators for <see cref="StoryEventDto.EventType"/>.
/// Movement types intentionally mirror Inventory's StockMovementTypes values.
/// </summary>
public static class StoryEventTypes
{
    public const string Purchase = "Purchase";
    public const string Sale = "Sale";
    public const string TransferIn = "TransferIn";
    public const string TransferOut = "TransferOut";
    public const string Adjustment = "Adjustment";
    public const string WriteOff = "WriteOff";
    public const string Decision = "Decision";
    public const string Batch = "Batch";
}
