using System;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;

namespace Inventory.StockBatches;

/// <summary>
/// Factory + FEFO bookkeeping for the best-effort <see cref="AppStockBatch"/> ledger.
/// The entity constructor is internal, so batches are only ever created through here.
/// IMPORTANT: this ledger is advisory — AppBranchInventory.QuantityOnHand remains the
/// single source of truth for stock decisions; callers must treat any batch drift as
/// acceptable (see BranchInventoryManager.TrackBatchLedgerBestEffortAsync).
/// </summary>
public class StockBatchManager : DomainService
{
    private readonly IStockBatchRepository _batchRepository;

    /// <summary>
    /// Last sequence issued by this (transient, per-scope) instance so several
    /// batches created inside one unit of work (e.g. receiving a multi-line PO)
    /// don't all read the same persisted Max and collide.
    /// </summary>
    private string? _lastIssuedPrefix;
    private int _lastIssuedSeq;

    public StockBatchManager(IStockBatchRepository batchRepository)
    {
        _batchRepository = batchRepository;
    }

    /// <summary>
    /// Creates and inserts one batch with an explicit expiry date.
    /// Batch numbers follow the document-number convention (B-YYYYMMDD-NNNN,
    /// sequence resets per day — mirrors SaleManager's INV-YYYY-NNNN generator).
    /// </summary>
    public async Task<AppStockBatch> CreateAsync(
        Guid branchId,
        Guid productId,
        int quantity,
        DateTime expiryDate,
        string sourceType,
        Guid? sourceId = null,
        bool autoSave = false)
    {
        var batchNumber = await GenerateBatchNumberAsync();

        var batch = new AppStockBatch(
            GuidGenerator.Create(),
            branchId,
            productId,
            batchNumber,
            expiryDate,
            quantity,
            sourceType,
            sourceId);

        await _batchRepository.InsertAsync(batch, autoSave);
        return batch;
    }

    /// <summary>
    /// Creates a batch whose expiry is today (UTC) + the product's shelf life.
    /// </summary>
    public Task<AppStockBatch> ReceiveAsync(
        Guid branchId,
        Guid productId,
        int quantity,
        int shelfLifeDays,
        string sourceType,
        Guid? sourceId = null)
    {
        var expiryDate = Clock.Now.Date.AddDays(shelfLifeDays);
        return CreateAsync(branchId, productId, quantity, expiryDate, sourceType, sourceId);
    }

    /// <summary>
    /// FEFO consumption: walks the product+branch's live batches — non-expired ones
    /// first (earliest expiry first), then expired ones (oldest first, so dead stock
    /// is cleared from the ledger too) — consuming up to <paramref name="quantity"/>.
    /// When <paramref name="expiredFirst"/> is true (waste write-offs) the order is
    /// inverted: EXPIRED batches first (oldest expiry first), then the normal FEFO
    /// order — a write-off must clear the dead stock before touching sellable lots.
    /// BEST-EFFORT BY DESIGN: if the batches cover less than the requested quantity
    /// it consumes what exists and returns the consumed amount — it never throws for
    /// a shortfall, because the authoritative stock mutation must not be blocked by
    /// ledger drift.
    /// </summary>
    public async Task<int> ConsumeFefoAsync(Guid branchId, Guid productId, int quantity, bool expiredFirst = false)
    {
        if (quantity <= 0)
        {
            return 0;
        }

        var batches = await _batchRepository.GetOpenBatchesAsync(branchId, productId);
        if (batches.Count == 0)
        {
            return 0;
        }

        // GetOpenBatchesAsync already orders by ExpiryDate ascending, so both
        // partitions below keep "oldest expiry first" within themselves.
        var today = Clock.Now.Date;
        var fefoOrder = expiredFirst
            ? batches.Where(b => b.IsExpired(today)).Concat(batches.Where(b => !b.IsExpired(today)))
            : batches.Where(b => !b.IsExpired(today)).Concat(batches.Where(b => b.IsExpired(today)));

        var remaining = quantity;
        foreach (var batch in fefoOrder)
        {
            if (remaining <= 0)
            {
                break;
            }

            var take = Math.Min(remaining, batch.QuantityRemaining);
            batch.Consume(take);
            await _batchRepository.UpdateAsync(batch);
            remaining -= take;
        }

        if (remaining > 0)
        {
            Logger.LogDebug(
                "StockBatchManager: batch ledger drift — needed {Quantity} but batches only covered {Covered} " +
                "for product {ProductId} at branch {BranchId}. The authoritative stock row is unaffected.",
                quantity, quantity - remaining, productId, branchId);
        }

        return quantity - remaining;
    }

    private async Task<string> GenerateBatchNumberAsync()
    {
        var prefix = $"B-{Clock.Now:yyyyMMdd}-";

        var queryable = await _batchRepository.GetQueryableAsync();
        var lastNumber = await AsyncExecuter.MaxAsync(
            queryable.Where(b => b.BatchNumber.StartsWith(prefix)).Select(b => (string?)b.BatchNumber),
            x => x);

        var nextSeq = 1;
        if (!string.IsNullOrEmpty(lastNumber))
        {
            var tail = lastNumber.Substring(prefix.Length);
            if (int.TryParse(tail, out var n))
            {
                nextSeq = n + 1;
            }
        }

        // Uncommitted inserts from this unit of work are invisible to the Max query —
        // bump past the last number this instance already issued for the same day.
        if (_lastIssuedPrefix == prefix && nextSeq <= _lastIssuedSeq)
        {
            nextSeq = _lastIssuedSeq + 1;
        }

        _lastIssuedPrefix = prefix;
        _lastIssuedSeq = nextSeq;
        return $"{prefix}{nextSeq:D4}";
    }
}
