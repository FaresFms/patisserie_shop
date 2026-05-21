using System;
using Volo.Abp.Domain.Entities;

namespace Operations.Entities;

public class AppSaleItem : Entity<Guid>
{
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }

    protected AppSaleItem() { }

    public AppSaleItem(Guid id, Guid saleId, Guid productId, int quantity, decimal unitPrice)
        : base(id)
    {
        SaleId = saleId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Subtotal = quantity * unitPrice;
    }
}
