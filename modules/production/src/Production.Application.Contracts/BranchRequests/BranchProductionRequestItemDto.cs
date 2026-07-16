using System;

namespace Production.BranchRequests;

public class BranchProductionRequestItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductSku { get; set; }
    public string? ProductUnit { get; set; }
    public int RequestedQuantity { get; set; }
    public int ApprovedQuantity { get; set; }
    public int PlannedQuantity { get; set; }
    public int FulfilledQuantity { get; set; }
    public string? Notes { get; set; }
}
