using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using Operations.Entities;
using Operations.PurchaseOrders;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Operations.EntityFrameworkCore;

public class PurchaseOrderRepository
    : EfCoreRepository<OperationsDbContext, AppPurchaseOrder, Guid>,
      IPurchaseOrderRepository
{
    public PurchaseOrderRepository(IDbContextProvider<OperationsDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<AppPurchaseOrder> GetWithItemsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = await WithDetailsAsync(p => p.Items);
        return await query.FirstOrDefaultAsync(p => p.Id == id, GetCancellationToken(cancellationToken))
            ?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(AppPurchaseOrder), id);
    }

    public async Task<long> CountFilteredAsync(
        string? filter,
        string? status,
        Guid? supplierId,
        Guid? destBranchId,
        DateTime? fromDate,
        DateTime? toDate,
        List<Guid>? visibleBranchIds,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(
            filter, status, supplierId, destBranchId, fromDate, toDate, visibleBranchIds);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<AppPurchaseOrder>> GetFilteredListAsync(
        string? filter,
        string? status,
        Guid? supplierId,
        Guid? destBranchId,
        DateTime? fromDate,
        DateTime? toDate,
        List<Guid>? visibleBranchIds,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(
            filter, status, supplierId, destBranchId, fromDate, toDate, visibleBranchIds);
        return await query
            .Include(p => p.Items)
            .OrderBy(ResolveSorting(sorting))
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<AppPurchaseOrder>> BuildFilteredQueryAsync(
        string? filter,
        string? status,
        Guid? supplierId,
        Guid? destBranchId,
        DateTime? fromDate,
        DateTime? toDate,
        List<Guid>? visibleBranchIds)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(status) && PurchaseOrderStatuses.All.Contains(status))
            query = query.Where(p => p.Status == status);
        if (supplierId.HasValue) query = query.Where(p => p.SupplierId == supplierId.Value);
        if (destBranchId.HasValue) query = query.Where(p => p.DestBranchId == destBranchId.Value);
        if (fromDate.HasValue) query = query.Where(p => p.OrderDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(p => p.OrderDate <= toDate.Value);
        if (visibleBranchIds != null) query = query.Where(p => visibleBranchIds.Contains(p.DestBranchId));

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var normalized = filter.Trim().ToLower();
            query = query.Where(p => p.PONumber.ToLower().Contains(normalized)
                || (p.Notes != null && p.Notes.ToLower().Contains(normalized)));
        }

        return query;
    }

    private static string ResolveSorting(string? sorting)
    {
        const string fallback = $"{nameof(AppPurchaseOrder.OrderDate)} desc";
        if (string.IsNullOrWhiteSpace(sorting)) return fallback;

        var value = sorting.Trim()
            .Replace("SupplierName", nameof(AppPurchaseOrder.SupplierId), StringComparison.OrdinalIgnoreCase)
            .Replace("DestBranchName", nameof(AppPurchaseOrder.DestBranchId), StringComparison.OrdinalIgnoreCase);
        var column = value.Split(' ')[0];
        var allowed = new[]
        {
            nameof(AppPurchaseOrder.PONumber), nameof(AppPurchaseOrder.Status),
            nameof(AppPurchaseOrder.SupplierId), nameof(AppPurchaseOrder.DestBranchId),
            nameof(AppPurchaseOrder.OrderDate), nameof(AppPurchaseOrder.ExpectedDeliveryDate),
            nameof(AppPurchaseOrder.ActualDeliveryDate), nameof(AppPurchaseOrder.TotalAmount),
            nameof(AppPurchaseOrder.CreationTime)
        };
        return allowed.Contains(column, StringComparer.OrdinalIgnoreCase) ? value : fallback;
    }

    public async Task<List<SupplierScorecardRow>> GetSupplierScorecardsAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        IReadOnlyCollection<Guid>? supplierIds,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);
        var dbContext = await GetDbContextAsync();

        // Only received/terminal orders carry delivery-performance signal.
        var orders = dbContext.Set<AppPurchaseOrder>()
            .Where(o => o.OrderDate >= fromUtc && o.OrderDate < toUtcExclusive)
            .Where(o => o.Status == PurchaseOrderStatuses.Received
                     || o.Status == PurchaseOrderStatuses.PartialReceived);

        if (supplierIds != null)
        {
            orders = orders.Where(o => supplierIds.Contains(o.SupplierId));
        }

        // Header-level facts, projected flat (date arithmetic is done client-side after
        // materializing — avoids leaning on Npgsql DateTime-subtraction translation and
        // keeps the query trivially translatable).
        var headers = await orders
            .Select(o => new HeaderProjection
            {
                SupplierId = o.SupplierId,
                Status = o.Status,
                OrderDate = o.OrderDate,
                ExpectedDeliveryDate = o.ExpectedDeliveryDate,
                ActualDeliveryDate = o.ActualDeliveryDate
            })
            .ToListAsync(ct);

        // Item quantity sums grouped by supplier (one translatable GroupBy over the
        // items joined back to their — already filtered — orders).
        var itemRows = from o in orders
                       from i in o.Items
                       select new { o.SupplierId, i.OrderedQuantity, i.ReceivedQuantity };

        var itemSums = await itemRows
            .GroupBy(x => x.SupplierId)
            .Select(g => new ItemSumProjection
            {
                SupplierId = g.Key,
                TotalOrderedQty = g.Sum(x => x.OrderedQuantity),
                TotalReceivedQty = g.Sum(x => x.ReceivedQuantity)
            })
            .ToListAsync(ct);

        var itemSumBySupplier = itemSums.ToDictionary(s => s.SupplierId);

        // Compose per supplier in memory — the header set is small (terminal POs in a
        // ≤180-day window) and avoids any date-diff translation concern.
        return headers
            .GroupBy(h => h.SupplierId)
            .Select(g =>
            {
                var both = g
                    .Where(h => h.ExpectedDeliveryDate.HasValue && h.ActualDeliveryDate.HasValue)
                    .ToList();

                var delaysDays = both
                    .Select(h => (h.ActualDeliveryDate!.Value - h.ExpectedDeliveryDate!.Value).TotalDays)
                    .ToList();

                var leadTimes = g
                    .Where(h => h.Status == PurchaseOrderStatuses.Received && h.ActualDeliveryDate.HasValue)
                    .Select(h => (h.ActualDeliveryDate!.Value - h.OrderDate).TotalDays)
                    .ToList();

                itemSumBySupplier.TryGetValue(g.Key, out var sums);

                return new SupplierScorecardRow
                {
                    SupplierId = g.Key,
                    TotalOrders = g.Count(),
                    ReceivedOrders = g.Count(h => h.Status == PurchaseOrderStatuses.Received),
                    OnTimeOrders = both.Count(h => h.ActualDeliveryDate!.Value <= h.ExpectedDeliveryDate!.Value),
                    LateOrders = both.Count(h => h.ActualDeliveryDate!.Value > h.ExpectedDeliveryDate!.Value),
                    AvgDelayDays = delaysDays.Count > 0 ? delaysDays.Average() : (double?)null,
                    TotalOrderedQty = sums?.TotalOrderedQty ?? 0,
                    TotalReceivedQty = sums?.TotalReceivedQty ?? 0,
                    AvgActualLeadTimeDays = leadTimes.Count > 0 ? leadTimes.Average() : (double?)null,
                    LeadTimeSampleSize = leadTimes.Count
                };
            })
            .ToList();
    }

    private sealed class HeaderProjection
    {
        public Guid SupplierId { get; set; }
        public string Status { get; set; } = null!;
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public DateTime? ActualDeliveryDate { get; set; }
    }

    private sealed class ItemSumProjection
    {
        public Guid SupplierId { get; set; }
        public int TotalOrderedQty { get; set; }
        public int TotalReceivedQty { get; set; }
    }
}
