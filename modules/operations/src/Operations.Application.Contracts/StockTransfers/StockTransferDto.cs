using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Operations.StockTransfers;

public class StockTransferDto : EntityDto<Guid>
{
    /// <summary>Human-friendly reference derived from the Id (no separate column).</summary>
    public string Reference { get; set; } = null!;
    public string Status { get; set; } = null!;
    public Guid FromBranchId { get; set; }
    public string FromBranchName { get; set; } = null!;
    public Guid ToBranchId { get; set; }
    public string ToBranchName { get; set; } = null!;
    public DateTime RequestedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public string? RequestedByUserName { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByUserName { get; set; }
    public string? Notes { get; set; }
    public DateTime CreationTime { get; set; }
    public int ItemCount { get; set; }
    public List<StockTransferItemDto> Items { get; set; } = new();
}
