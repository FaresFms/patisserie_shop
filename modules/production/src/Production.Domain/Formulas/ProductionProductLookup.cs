using System;

namespace Production.Formulas;

/// <summary>
/// Lightweight product read-model for the formula screen's two dropdowns (producible
/// finished products, and eligible ingredients). Carries Unit + CostPrice so the UI
/// can label quantities and preview costs without a second round-trip. Returned by
/// <see cref="IProductionFormulaRepository"/> so no Inventory queryable leaks into the
/// production app service.
/// </summary>
public class ProductionProductLookup
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string SKU { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public string ProductType { get; set; } = null!;
    public decimal CostPrice { get; set; }
    public string Currency { get; set; } = "USD";
}
