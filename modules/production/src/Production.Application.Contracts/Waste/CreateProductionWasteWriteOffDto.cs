using System;

namespace Production.Waste;

public class CreateProductionWasteWriteOffDto
{
    public Guid KitchenBranchId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = ProductionWasteReasons.Other;
    public string? Notes { get; set; }
}
