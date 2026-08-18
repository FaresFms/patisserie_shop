using System;

namespace Production.Orders;

/// <summary>
/// One-click "produce this request line" — creates a cook order for a single
/// approved branch-request item without going through the demand-planning plan flow.
/// </summary>
public class CreateProductionOrderForRequestDto
{
    public Guid KitchenBranchId { get; set; }
    public Guid RequestId { get; set; }
    public Guid RequestItemId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}

public class ProductionOrderForRequestResultDto
{
    public Guid ProductionOrderId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int PlannedOutputQuantity { get; set; }
    public bool HasIngredientShortage { get; set; }

    /// <summary>
    /// True when the order was auto-scheduled and has its ingredients, so the baker can
    /// press Start with no further setup.
    /// </summary>
    public bool ReadyToStart { get; set; }
}
