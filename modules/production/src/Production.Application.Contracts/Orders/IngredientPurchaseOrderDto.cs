using System;

namespace Production.Orders;

public class IngredientPurchaseOrderDto
{
    public Guid PurchaseOrderId { get; set; }
    public string PONumber { get; set; } = null!;
    public string SupplierName { get; set; } = null!;
    public int LineCount { get; set; }
}
