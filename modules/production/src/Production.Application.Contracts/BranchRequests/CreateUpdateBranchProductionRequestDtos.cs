using System;
using System.Collections.Generic;

namespace Production.BranchRequests;

public class CreateBranchProductionRequestDto
{
    public Guid BranchId { get; set; }
    public DateTime NeededByDate { get; set; } = DateTime.Today.AddDays(1);
    public string Priority { get; set; } = ProductionPriorities.Normal;
    public string? Notes { get; set; }
    public List<CreateBranchProductionRequestItemDto> Items { get; set; } = new();
}

public class UpdateBranchProductionRequestDto
{
    public DateTime NeededByDate { get; set; }
    public string Priority { get; set; } = ProductionPriorities.Normal;
    public string? Notes { get; set; }
    public List<CreateBranchProductionRequestItemDto> Items { get; set; } = new();
}

public class CreateBranchProductionRequestItemDto
{
    public Guid ProductId { get; set; }
    public int RequestedQuantity { get; set; }
    public string? Notes { get; set; }
}

public class ApproveBranchProductionRequestDto
{
    public string? Reason { get; set; }
    public List<ApproveBranchProductionRequestItemDto> Items { get; set; } = new();
}

public class ApproveBranchProductionRequestItemDto
{
    public Guid ItemId { get; set; }
    public int ApprovedQuantity { get; set; }
}

public class RejectBranchProductionRequestDto
{
    public string Reason { get; set; } = string.Empty;
}
