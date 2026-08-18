using System;
using Volo.Abp.Application.Dtos;

namespace Production.Orders;

public class ProductionOrderListItemDto : EntityDto<Guid>
{
    public string OrderNumber { get; set; } = null!;
    public Guid KitchenBranchId { get; set; }
    public string KitchenBranchName { get; set; } = null!;
    public Guid FinishedProductId { get; set; }
    public string FinishedProductName { get; set; } = null!;
    public string FinishedProductSku { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public string Status { get; set; } = ProductionOrderStatuses.Draft;
    public string Priority { get; set; } = ProductionPriorities.Normal;
    public int PlannedOutputQuantity { get; set; }
    public int AcceptedQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int DispatchedQuantity { get; set; }
    public int InTransitQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public int LostQuantity { get; set; }
    public int RemainingToDispatch { get; set; }

    /// <summary>
    /// Non-expired finished-product stock actually sitting in the kitchen right now.
    /// <see cref="RemainingToDispatch"/> is an allocation promise; this is what can
    /// physically ship today, so the UI can warn before a transfer is attempted.
    /// </summary>
    public int UsableKitchenStock { get; set; }
    public DateTime? ActualStartTime { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal TotalProductionCost { get; set; }
    public string QualityStatus { get; set; } = ProductionQualityStatuses.NotRequired;
    public string? QualityReason { get; set; }
    public DateTime? QualityUpdatedAt { get; set; }
    public string? OutputBatchNumber { get; set; }
    public string? WorkCenterCode { get; set; }
    public string? ShiftCode { get; set; }
    public string? AssignedOperatorName { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? ScheduledEndTime { get; set; }
}
