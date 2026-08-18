using System;
using System.Collections.Generic;

namespace Production.Formulas;

public class ProductionFormulaDto
{
    public Guid Id { get; set; }
    public Guid FinishedProductId { get; set; }

    /// <summary>Joined from Inventory — resolved by the app service, not the entity.</summary>
    public string? FinishedProductName { get; set; }

    /// <summary>Joined from Inventory — the finished product's unit-of-measure label.</summary>
    public string? FinishedProductUnit { get; set; }

    public string FormulaName { get; set; } = null!;
    public int Version { get; set; }
    public int OutputQuantity { get; set; }
    public decimal ExpectedWastePercent { get; set; }
    public decimal LaborCostPerBatch { get; set; }
    public decimal OverheadCostPerBatch { get; set; }
    public int EstimatedProductionMinutes { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public string? Notes { get; set; }
    public string ApprovalStatus { get; set; } = ProductionFormulaStatuses.Draft;
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public bool IsEditable { get; set; }
    public string? WorkCenterCode { get; set; }
    public string? PreparationSteps { get; set; }

    public List<ProductionFormulaItemDto> Items { get; set; } = new();
}
