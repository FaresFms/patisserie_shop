using System;
using System.Collections.Generic;
using System.Linq;
using Operations.Events;
using Volo.Abp.Domain.Entities.Auditing;

namespace Operations.Entities;

public class AppSale : FullAuditedAggregateRoot<Guid>
{
    public Guid BranchId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime SaleDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Notes { get; set; }

    public ICollection<AppSaleItem> Items { get; private set; }

    protected AppSale()
    {
        Items = new List<AppSaleItem>();
    }

    public AppSale(
        Guid id,
        Guid branchId,
        string invoiceNumber,
        DateTime saleDate,
        string currency = "USD",
        string? notes = null)
        : base(id)
    {
        BranchId = branchId;
        InvoiceNumber = invoiceNumber;
        SaleDate = saleDate;
        Currency = currency;
        Notes = notes;
        Items = new List<AppSaleItem>();
        TotalAmount = 0m;
    }

    public AppSaleItem AddItem(Guid productId, int qty, decimal unitPrice)
    {
        var item = new AppSaleItem(Guid.NewGuid(), Id, productId, qty, unitPrice);
        Items.Add(item);
        TotalAmount = Items.Sum(i => i.Subtotal);

        AddDistributedEvent(new SaleRecordedEto
        {
            SaleId = Id,
            BranchId = BranchId
        });

        return item;
    }
}
