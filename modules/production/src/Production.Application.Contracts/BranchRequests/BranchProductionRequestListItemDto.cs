using System;
using Volo.Abp.Application.Dtos;

namespace Production.BranchRequests;

public class BranchProductionRequestListItemDto : EntityDto<Guid>
{
    public string RequestNumber { get; set; } = null!;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = null!;
    public DateTime NeededByDate { get; set; }
    public string Priority { get; set; } = ProductionPriorities.Normal;
    public string Status { get; set; } = BranchProductionRequestStatuses.Draft;
    public int ItemCount { get; set; }
    public int RequestedTotalQuantity { get; set; }
    public int ApprovedTotalQuantity { get; set; }
    public int PlannedTotalQuantity { get; set; }
    public int FulfilledTotalQuantity { get; set; }
}
