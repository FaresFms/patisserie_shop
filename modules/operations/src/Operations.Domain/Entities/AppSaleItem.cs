using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Operations.Entities;

public class AppSaleItem : Entity<Guid>
{
    public Guid SaleId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Subtotal { get; private set; }
    /// <summary>
    /// Expiry lots consumed by this sale line, serialized with
    /// <see cref="StockTransferBatchBreakdown"/>. Used to restore the exact lots on void.
    /// </summary>
    public string? SoldBatchBreakdown { get; private set; }

    protected AppSaleItem() { }

    internal AppSaleItem(Guid id, Guid saleId, Guid productId, int quantity, decimal unitPrice)
        : base(id)
    {
        if (quantity <= 0)
            throw new BusinessException(OperationsErrorCodes.InvalidQuantity);
        if (unitPrice < 0)
            throw new BusinessException(OperationsErrorCodes.InvalidPrice);

        SaleId = saleId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Subtotal = quantity * unitPrice;
    }

    internal void SetSoldBatchBreakdown(string? breakdown)
    {
        SoldBatchBreakdown = breakdown;
    }
}
