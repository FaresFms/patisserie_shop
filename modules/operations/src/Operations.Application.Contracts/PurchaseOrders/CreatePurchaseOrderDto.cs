using System;

namespace Operations.PurchaseOrders;

public class CreatePurchaseOrderDto
{
    public Guid SupplierId { get; set; }
    public Guid DestBranchId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.Today;
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Notes { get; set; }
}

public class UpdatePurchaseOrderHeaderDto
{
    public Guid SupplierId { get; set; }
    public Guid DestBranchId { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
}
