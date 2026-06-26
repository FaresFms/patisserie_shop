using System;

namespace Production.Orders;

public class ProductionOrderIngredientDto
{
    public Guid Id { get; set; }
    public Guid IngredientProductId { get; set; }
    public string? IngredientName { get; set; }
    public string? IngredientSku { get; set; }
    public string? Unit { get; set; }
    public int RequiredQuantity { get; set; }
    public int ConsumedQuantity { get; set; }
    public decimal UnitCostSnapshot { get; set; }
    public decimal TotalCost { get; set; }
}
