using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Production.Orders;

public class ProductionOrderDto : EntityDto<Guid>
{
    public string OrderNumber { get; set; } = null!;
    public Guid KitchenBranchId { get; set; }
    public string? KitchenBranchName { get; set; }
    public Guid? ProductionPlanId { get; set; }
    public Guid? ProductionPlanLineId { get; set; }
    public Guid FinishedProductId { get; set; }
    public string? FinishedProductName { get; set; }
    public string? FinishedProductSku { get; set; }
    public string? FinishedProductUnit { get; set; }
    public Guid FormulaId { get; set; }
    public int FormulaVersion { get; set; }
    public string Status { get; set; } = ProductionOrderStatuses.Draft;
    public string Priority { get; set; } = ProductionPriorities.Normal;
    public int PlannedOutputQuantity { get; set; }
    public int ActualOutputQuantity { get; set; }
    public int AcceptedQuantity { get; set; }
    public int RejectedQuantity { get; set; }
    public DateTime? PlannedStartTime { get; set; }
    public DateTime? ActualStartTime { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal PlannedIngredientCost { get; set; }
    public decimal ActualIngredientCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal OverheadCost { get; set; }
    public decimal TotalProductionCost { get; set; }
    public decimal UnitProductionCost { get; set; }
    public string? WasteReason { get; set; }
    public string? Notes { get; set; }
    public List<ProductionOrderIngredientDto> Ingredients { get; set; } = new();
    public List<ProductionIngredientAvailabilityDto> Availability { get; set; } = new();
}
