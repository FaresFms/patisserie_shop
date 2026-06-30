using System;

namespace Production.Formulas;

/// <summary>
/// Lightweight product option for the formula screen's dropdowns. Production-local
/// (the contracts project does not reference Inventory.Application.Contracts). Carries
/// Unit + CostPrice so the editor can label quantities and preview costs.
/// </summary>
public class ProductLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string SKU { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public string ProductType { get; set; } = null!;
    public decimal CostPrice { get; set; }
    public string Currency { get; set; } = "USD";
}
