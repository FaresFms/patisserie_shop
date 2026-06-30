using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Production.BranchRequests;

public class BranchProductionRequestDto : EntityDto<Guid>
{
    public string RequestNumber { get; set; } = null!;
    public Guid BranchId { get; set; }
    public string? BranchName { get; set; }
    public DateTime NeededByDate { get; set; }
    public string Priority { get; set; } = ProductionPriorities.Normal;
    public string Status { get; set; } = BranchProductionRequestStatuses.Draft;
    public Guid? RequestedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Notes { get; set; }
    public string? DecisionReason { get; set; }
    public string? RejectionReason { get; set; }
    public List<BranchProductionRequestItemDto> Items { get; set; } = new();
}
