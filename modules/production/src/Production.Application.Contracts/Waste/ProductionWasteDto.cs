using System;
using Volo.Abp.Application.Dtos;

namespace Production.Waste;

public class ProductionWasteDto : EntityDto<Guid>
{
    public Guid? ProductionOrderId { get; set; }
    public string? ProductionOrderNumber { get; set; }
    public Guid KitchenBranchId { get; set; }
    public string KitchenBranchName { get; set; } = null!;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSku { get; set; } = null!;
    public string ProductUnit { get; set; } = null!;
    public string WasteType { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string Reason { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTime RecordedAt { get; set; }
}
