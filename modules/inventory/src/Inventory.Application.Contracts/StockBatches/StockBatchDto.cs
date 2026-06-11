using System;
using Volo.Abp.Application.Dtos;

namespace Inventory.StockBatches;

public class StockBatchDto : EntityDto<Guid>
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public string BatchNumber { get; set; } = null!;
    public DateTime ExpiryDate { get; set; }
    public int QuantityReceived { get; set; }
    public int QuantityRemaining { get; set; }
    public string SourceType { get; set; } = null!;
    public Guid? SourceId { get; set; }
    public DateTime CreationTime { get; set; }

    // Joined fields (populated from the read model, not mapped).
    public string ProductName { get; set; } = null!;
    public string ProductSKU { get; set; } = null!;
    public string ProductUnit { get; set; } = null!;
    public string BranchName { get; set; } = null!;
}
