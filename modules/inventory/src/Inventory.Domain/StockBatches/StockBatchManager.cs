using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;

namespace Inventory.StockBatches;

/// <summary>
/// Factory + FEFO bookkeeping for the <see cref="AppStockBatch"/> expiry ledger.
/// The entity constructor is internal, so batches are only ever created through here.
/// AppBranchInventory.QuantityOnHand remains the total stock figure, while perishable
/// sale/production/transfer availability is capped by live non-expired batches.
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
    /// Non-perishable products use the authoritative stock row. Perishable products
    /// may only use units backed by a live, non-expired batch; any ledger drift is
    /// quarantined instead of being treated as fresh stock.
    /// </summary>
    public static int GetUsableQuantity(
        AppProduct product,
        AppBranchInventory inventory,
        IReadOnlyDictionary<Guid, int> nonExpiredByProduct)
    {
        if (!product.ShelfLifeDays.HasValue)
        {
            return inventory.QuantityOnHand;
        }

        var tracked = nonExpiredByProduct.GetValueOrDefault(product.Id);
        return Math.Min(inventory.QuantityOnHand, tracked);
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
    /// first (earliest expiry first), optionally followed by expired ones.
    /// When <paramref name="expiredFirst"/> is true (waste write-offs) the order is
    /// inverted: EXPIRED batches first (oldest expiry first), then the normal FEFO
    /// order — a write-off must clear the dead stock before touching sellable lots.
    /// BEST-EFFORT BY DESIGN: if the batches cover less than the requested quantity
    /// it consumes what exists and returns the consumed amount — it never throws for
    /// a shortfall, because the authoritative stock mutation must not be blocked by
    /// ledger drift.
    /// </summary>
    public async Task<int> ConsumeFefoAsync(
        Guid branchId,
        Guid productId,
        int quantity,
        bool expiredFirst = false,
        bool includeExpiredFallback = true)
    {
        var consumed = await ConsumeFefoWithBreakdownAsync(
            branchId, productId, quantity, expiredFirst, includeExpiredFallback);
        return consumed.Sum(x => x.Quantity);
    }

    /// <summary>
    /// FEFO consumption with the consumed batch split. This is used by transfers so
    /// the destination branch receives stock with the same expiry dates as the
    /// source lots.
    /// </summary>
    public async Task<List<ConsumedStockBatchLine>> ConsumeFefoWithBreakdownAsync(
        Guid branchId,
        Guid productId,
        int quantity,
        bool expiredFirst = false,
        bool includeExpiredFallback = true)
    {
        if (quantity <= 0)
        {
            return new List<ConsumedStockBatchLine>();
        }

        var batches = await _batchRepository.GetOpenBatchesAsync(branchId, productId);
        if (batches.Count == 0)
        {
            return new List<ConsumedStockBatchLine>();
        }

        // GetOpenBatchesAsync already orders by ExpiryDate ascending, so both
        // partitions below keep "oldest expiry first" within themselves.
        var today = Clock.Now.Date;
        var usable = batches.Where(b => !b.IsExpired(today));
        var expired = batches.Where(b => b.IsExpired(today));
        var fefoOrder = expiredFirst
            ? expired.Concat(usable)
            : includeExpiredFallback
                ? usable.Concat(expired)
                : usable;

        var remaining = quantity;
        var consumed = new List<ConsumedStockBatchLine>();
        foreach (var batch in fefoOrder)
        {
            if (remaining <= 0)
            {
                break;
            }

            var take = Math.Min(remaining, batch.QuantityRemaining);
            var expiryDate = batch.ExpiryDate;
            batch.Consume(take);
            await _batchRepository.UpdateAsync(batch);
            consumed.Add(new ConsumedStockBatchLine(batch.Id, expiryDate, take));
            remaining -= take;
        }

        if (remaining > 0)
        {
            Logger.LogDebug(
                "StockBatchManager: batch ledger drift — needed {Quantity} but batches only covered {Covered} " +
                "for product {ProductId} at branch {BranchId}. The authoritative stock row is unaffected.",
                quantity, quantity - remaining, productId, branchId);
        }

        return consumed;
    }

    private async Task<string> GenerateBatchNumberAsync()
    {
        var prefix = $"B-{Clock.Now:yyyyMMdd}-";

        var queryable = await _batchRepository.GetQueryableAsync();

        // Max by PARSED numeric tail, not a lexical string Max: past 9999 the wider
        // tail sorts before the narrower one lexically and would reissue a duplicate
        // batch number for the day. Integer max over the day's suffixes is correct at
        // any width (the daily count is tiny for a patisserie).
        var numbers = await AsyncExecuter.ToListAsync(
            queryable.Where(b => b.BatchNumber.StartsWith(prefix)).Select(b => b.BatchNumber));

        var nextSeq = 1;
        foreach (var number in numbers)
        {
            if (int.TryParse(number.Substring(prefix.Length), out var n) && n >= nextSeq)
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
