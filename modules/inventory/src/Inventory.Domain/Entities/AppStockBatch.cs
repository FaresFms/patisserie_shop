using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

/// <summary>
/// A received lot of a perishable product at a branch, tracked best-effort in
/// parallel to <see cref="AppBranchInventory"/>. The batch ledger powers FEFO
/// (first-expired-first-out) consumption and the ExpiringSoon intelligence rule;
/// it is NEVER the source of truth for stock decisions —
/// AppBranchInventory.QuantityOnHand is. Depleted rows (QuantityRemaining == 0)
/// are kept for traceability; queries filter on QuantityRemaining &gt; 0.
/// </summary>
public class AppStockBatch : AuditedAggregateRoot<Guid>
{
    public Guid BranchId { get; private set; }
    public Guid ProductId { get; private set; }
    public string BatchNumber { get; private set; } = null!;

    /// <summary>Date-precision: the last day the batch is still sellable.</summary>
    public DateTime ExpiryDate { get; private set; }

    public int QuantityReceived { get; private set; }
    public int QuantityRemaining { get; private set; }

    /// <summary>See <see cref="StockBatchSourceTypes"/>.</summary>
    public string SourceType { get; private set; } = null!;

    /// <summary>Id of the originating document (PO, transfer, …) when known.</summary>
    public Guid? SourceId { get; private set; }

    public bool IsDepleted => QuantityRemaining <= 0;

    /// <summary>Expired strictly after its expiry day — the expiry day itself still counts as sellable.</summary>
    public bool IsExpired(DateTime nowUtc) => ExpiryDate.Date < nowUtc.Date;

    protected AppStockBatch() { }

    internal AppStockBatch(
        Guid id,
        Guid branchId,
        Guid productId,
        string batchNumber,
        DateTime expiryDate,
        int quantityReceived,
        string sourceType,
        Guid? sourceId = null)
        : base(id)
    {
        if (quantityReceived <= 0)
        {
            throw new BusinessException(InventoryErrorCodes.InvalidBatchQuantity)
                .WithData("Quantity", quantityReceived);
        }
        if (!StockBatchSourceTypes.IsValid(sourceType))
        {
            throw new BusinessException(InventoryErrorCodes.InvalidBatchSourceType)
                .WithData("SourceType", sourceType ?? "(null)");
        }

        BranchId = branchId;
        ProductId = productId;
        BatchNumber = Check.NotNullOrWhiteSpace(batchNumber, nameof(batchNumber), maxLength: 32);
        ExpiryDate = expiryDate.Date;
        QuantityReceived = quantityReceived;
        QuantityRemaining = quantityReceived;
        SourceType = sourceType;
        SourceId = sourceId;
    }

    /// <summary>
    /// Removes <paramref name="quantity"/> units from the batch (FEFO consumption).
    /// Throws unless 0 &lt; quantity ≤ QuantityRemaining — the caller is responsible
    /// for splitting a larger consumption across multiple batches.
    /// </summary>
    public void Consume(int quantity)
    {
        if (quantity <= 0 || quantity > QuantityRemaining)
        {
            throw new BusinessException(InventoryErrorCodes.InvalidBatchConsumeQuantity)
                .WithData("Quantity", quantity)
                .WithData("QuantityRemaining", QuantityRemaining);
        }

        QuantityRemaining -= quantity;
    }
}
