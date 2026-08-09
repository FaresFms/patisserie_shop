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
    public int ReservedQuantity { get; set; }
    public int DispatchedQuantity { get; set; }
    public int InTransitQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public int LostQuantity { get; set; }
    public int RemainingToDispatch { get; set; }
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
    public Guid? ParentProductionOrderId { get; set; }
    public string? WorkCenterCode { get; set; }
    public string? ShiftCode { get; set; }
    public Guid? AssignedOperatorUserId { get; set; }
    public string? AssignedOperatorName { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? ScheduledEndTime { get; set; }
    public string FormulaAllergens { get; set; } = string.Empty;
    public string QualityStatus { get; set; } = ProductionQualityStatuses.NotRequired;
    public string? QualityReason { get; set; }
    public DateTime? QualityUpdatedAt { get; set; }
    public Guid? QualityUpdatedByUserId { get; set; }
    public Guid? OutputBatchId { get; set; }
    public string? OutputBatchNumber { get; set; }
    public List<ProductionOrderIngredientDto> Ingredients { get; set; } = new();
    public List<ProductionIngredientLotDto> IngredientLots { get; set; } = new();
    public List<ProductionIngredientAvailabilityDto> Availability { get; set; } = new();
    public List<ProductionOrderAllocationDto> Allocations { get; set; } = new();
}
