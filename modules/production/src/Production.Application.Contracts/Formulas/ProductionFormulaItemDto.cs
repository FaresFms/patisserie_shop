using System;

namespace Production.Formulas;

public class ProductionFormulaItemDto
{
    public Guid Id { get; set; }
    public Guid IngredientProductId { get; set; }
    public int Quantity { get; set; }
    public decimal LossPercent { get; set; }
    public int SortOrder { get; set; }
    public string? Allergens { get; set; }
}
