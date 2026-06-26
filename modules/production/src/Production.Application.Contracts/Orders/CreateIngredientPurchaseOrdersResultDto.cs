using System.Collections.Generic;

namespace Production.Orders;

public class CreateIngredientPurchaseOrdersResultDto
{
    public int TotalShortageQuantity { get; set; }
    public int PurchaseOrderCount => PurchaseOrders.Count;
    public List<IngredientPurchaseOrderDto> PurchaseOrders { get; set; } = new();
}
