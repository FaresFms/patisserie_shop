using System;

namespace Production.Orders;

public class ProductionIngredientAvailabilityDto
{
    public Guid IngredientProductId { get; set; }
    public string IngredientName { get; set; } = null!;
    public string IngredientSku { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public int RequiredQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int ShortageQuantity { get; set; }
    public bool HasShortage { get; set; }
}
