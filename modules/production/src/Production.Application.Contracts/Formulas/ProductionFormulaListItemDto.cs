using System;

namespace Production.Formulas;

/// <summary>Row DTO for the formulas list grid (flat header + finished-product name + item count).</summary>
public class ProductionFormulaListItemDto
{
    public Guid Id { get; set; }
    public Guid FinishedProductId { get; set; }
    public string FinishedProductName { get; set; } = null!;
    public string FinishedProductUnit { get; set; } = null!;
    public string FormulaName { get; set; } = null!;
    public int Version { get; set; }
    public int OutputQuantity { get; set; }
    public decimal ExpectedWastePercent { get; set; }
    public decimal LaborCostPerBatch { get; set; }
    public decimal OverheadCostPerBatch { get; set; }
    public int EstimatedProductionMinutes { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public string ApprovalStatus { get; set; } = ProductionFormulaStatuses.Draft;
    public string? WorkCenterCode { get; set; }
    public int ItemCount { get; set; }
}
