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

    /// <summary>
    /// The cash-drawer session this sale was rung on, when sold through the cashier POS.
    /// Null for sales recorded through the back-office <c>SaleAppService</c>.
    /// </summary>
    public Guid? ShiftId { get; private set; }

    // ─── Void state (cash POS) ───
    public bool IsVoided { get; private set; }
    public DateTime? VoidedAt { get; private set; }
    public Guid? VoidedByUserId { get; private set; }
    public string? VoidReason { get; private set; }

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

    /// <summary>Links this sale to the cash-drawer session it was rung on.</summary>
    public void AssignShift(Guid shiftId)
    {
        ShiftId = shiftId;
    }

    public void EnsureCashTendered(decimal? cashTendered)
    {
        if (!cashTendered.HasValue || cashTendered.Value < TotalAmount)
        {
            throw new BusinessException(OperationsErrorCodes.CashTenderedInsufficient)
                .WithData("Tendered", cashTendered ?? 0m)
                .WithData("Total", TotalAmount);
        }
    }

    public void RecordItemSoldBatches(Guid itemId, string? batchBreakdown)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new BusinessException(OperationsErrorCodes.SaleItemNotFound)
                .WithData("ItemId", itemId);
        item.SetSoldBatchBreakdown(batchBreakdown);
    }

    /// <summary>
    /// Marks the sale as voided. Stock restoration is orchestrated by the app service
    /// (it reverses each line via the inventory manager); this method only flips the
    /// void state on the aggregate. Throws if the sale is already voided.
    /// </summary>
    public void Void(Guid userId, string? reason, DateTime whenUtc)
    {
        if (IsVoided)
        {
            throw new BusinessException(OperationsErrorCodes.SaleAlreadyVoided)
                .WithData("SaleId", Id);
        }

        IsVoided = true;
        VoidedByUserId = userId;
        VoidReason = reason;
        VoidedAt = whenUtc;
    }

    private void RecalculateTotal() => TotalAmount = _items.Sum(i => i.Subtotal);
}
