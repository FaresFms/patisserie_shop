using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Operations.Events;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Operations.Entities;

public class AppSale : FullAuditedAggregateRoot<Guid>
{
    public Guid BranchId { get; private set; }
    public string InvoiceNumber { get; private set; } = null!;
    public DateTime SaleDate { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string? Notes { get; private set; }

    private readonly List<AppSaleItem> _items = new();
    public IReadOnlyCollection<AppSaleItem> Items => new ReadOnlyCollection<AppSaleItem>(_items);

    protected AppSale() { }

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
        InvoiceNumber = Check.NotNullOrWhiteSpace(invoiceNumber, nameof(invoiceNumber));
        SaleDate = saleDate;
        Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.ToUpper();
        Notes = notes;
        TotalAmount = 0m;
    }

    public AppSaleItem AddItem(Guid itemId, Guid productId, int quantity, decimal unitPrice)
    {
        if (_items.Any(i => i.ProductId == productId))
        {
            throw new BusinessException(OperationsErrorCodes.DuplicateProductInSale)
                .WithData("ProductId", productId);
        }
        var item = new AppSaleItem(itemId, Id, productId, quantity, unitPrice);
        _items.Add(item);
        RecalculateTotal();
        return item;
    }

    /// <summary>
    /// Validates that each line has enough stock against the supplied snapshot
    /// (BranchInventory.QuantityOnHand keyed by ProductId), then raises the
    /// SaleRecordedEto distributed event. Called once after all items are added.
    /// </summary>
    public void Record(IReadOnlyDictionary<Guid, int> currentStocks)
    {
        if (_items.Count == 0)
            throw new BusinessException(OperationsErrorCodes.CannotRecordEmptySale);

        foreach (var item in _items)
        {
            if (!currentStocks.TryGetValue(item.ProductId, out var stock))
            {
                throw new BusinessException(OperationsErrorCodes.NoInventoryRow)
                    .WithData("ProductId", item.ProductId)
                    .WithData("BranchId", BranchId);
            }
            if (stock < item.Quantity)
            {
                throw new BusinessException(OperationsErrorCodes.InsufficientStock)
                    .WithData("ProductId", item.ProductId)
                    .WithData("Available", stock)
                    .WithData("Requested", item.Quantity);
            }
        }

        AddDistributedEvent(new SaleRecordedEto
        {
            SaleId = Id,
            BranchId = BranchId
        });
    }

    private void RecalculateTotal() => TotalAmount = _items.Sum(i => i.Subtotal);
}
