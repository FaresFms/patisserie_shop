using System;
using Volo.Abp.Application.Dtos;

namespace Inventory.StockMovements;

public class StockMovementDto : EntityDto<Guid>
{
    public DateTime CreationTime { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = null!;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSKU { get; set; } = null!;
    public string ProductUnit { get; set; } = null!;
    public string MovementType { get; set; } = null!;
    public int Quantity { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? ReferenceType { get; set; }
    public string? Notes { get; set; }
}
