using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Production.Entities;
using Production.Waste;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Production.EntityFrameworkCore;

public class ProductionWasteRepository
    : EfCoreRepository<ProductionDbContext, AppProductionWaste, Guid>,
      IProductionWasteRepository
{
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;

    public ProductionWasteRepository(
        IDbContextProvider<ProductionDbContext> dbContextProvider,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository)
        : base(dbContextProvider)
    {
        _branchRepository = branchRepository;
        _productRepository = productRepository;
    }

    public async Task<long> CountFilteredAsync(
        string? filter,
        string? wasteType,
        Guid? kitchenBranchId,
        DateTime? fromDate,
        DateTime? toDate,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, wasteType, kitchenBranchId, fromDate, toDate, scopedKitchenBranchIds);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<ProductionWasteListItem>> GetFilteredListAsync(
        string? filter,
        string? wasteType,
        Guid? kitchenBranchId,
        DateTime? fromDate,
        DateTime? toDate,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);
        var query = await BuildFilteredQueryAsync(filter, wasteType, kitchenBranchId, fromDate, toDate, scopedKitchenBranchIds);

        var rows = await ApplySorting(query, sorting)
            .Skip(skipCount)
            .Take(maxResultCount)
            .Select(w => new WasteProjection
            {
                Id = w.Id,
                ProductionOrderId = w.ProductionOrderId,
                KitchenBranchId = w.KitchenBranchId,
                ProductId = w.ProductId,
                WasteType = w.WasteType,
                Quantity = w.Quantity,
                UnitCost = w.UnitCost,
                TotalCost = w.TotalCost,
                Reason = w.Reason,
                Notes = w.Notes,
                RecordedAt = w.RecordedAt
            })
            .ToListAsync(ct);

        return await HydrateListItemsAsync(rows, ct);
    }

    public async Task<ProductionWasteAnalyticsReadModel> GetAnalyticsAsync(
        int days,
        Guid? kitchenBranchId,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);
        var now = DateTime.UtcNow.Date;
        var from = now.AddDays(-Math.Max(1, days) + 1);
        var query = await GetQueryableAsync();
        query = query.Where(w => w.RecordedAt.Date >= from && w.RecordedAt.Date <= now);
        if (kitchenBranchId.HasValue)
        {
            query = query.Where(w => w.KitchenBranchId == kitchenBranchId.Value);
        }
        query = query.Where(w => scopedKitchenBranchIds.Contains(w.KitchenBranchId));

        var rows = await query
            .Select(w => new WasteProjection
            {
                Id = w.Id,
                ProductId = w.ProductId,
                Reason = w.Reason,
                Quantity = w.Quantity,
                TotalCost = w.TotalCost,
                RecordedAt = w.RecordedAt
            })
            .ToListAsync(ct);

        var productIds = rows.Select(r => r.ProductId).Distinct().ToList();
        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id), cancellationToken: ct);
        var productById = products.ToDictionary(p => p.Id);

        var analytics = new ProductionWasteAnalyticsReadModel
        {
            Summary = new ProductionWasteSummary
            {
                TotalIncidents = rows.Count,
                TotalCost = rows.Sum(r => r.TotalCost),
                TopReason = rows.GroupBy(r => r.Reason)
                    .OrderByDescending(g => g.Sum(x => x.TotalCost))
                    .Select(g => g.Key)
                    .FirstOrDefault(),
                TopProductName = rows.GroupBy(r => r.ProductId)
                    .OrderByDescending(g => g.Sum(x => x.TotalCost))
                    .Select(g => productById.TryGetValue(g.Key, out var product) ? product.Name : null)
                    .FirstOrDefault()
            }
        };

        for (var date = from; date <= now; date = date.AddDays(1))
        {
            var dayRows = rows.Where(r => r.RecordedAt.Date == date).ToList();
            analytics.DailySeries.Add(new ProductionWasteDailyPoint
            {
                Date = date,
                IncidentCount = dayRows.Count,
                Cost = dayRows.Sum(r => r.TotalCost)
            });
        }

        analytics.Reasons = rows
            .GroupBy(r => r.Reason)
            .Select(g => new ProductionWasteReasonSlice
            {
                Reason = g.Key,
                IncidentCount = g.Count(),
                Cost = g.Sum(x => x.TotalCost)
            })
            .OrderByDescending(x => x.Cost)
            .ToList();

        analytics.TopProducts = rows
            .GroupBy(r => r.ProductId)
            .Select(g =>
            {
                productById.TryGetValue(g.Key, out var product);
                return new ProductionWasteProductRow
                {
                    ProductId = g.Key,
                    ProductName = product?.Name ?? g.Key.ToString(),
                    ProductSku = product?.SKU ?? string.Empty,
                    ProductUnit = product?.Unit ?? string.Empty,
                    Quantity = g.Sum(x => x.Quantity),
                    Cost = g.Sum(x => x.TotalCost)
                };
            })
            .OrderByDescending(x => x.Cost)
            .Take(8)
            .ToList();

        return analytics;
    }

    private async Task<IQueryable<AppProductionWaste>> BuildFilteredQueryAsync(
        string? filter,
        string? wasteType,
        Guid? kitchenBranchId,
        DateTime? fromDate,
        DateTime? toDate,
        IReadOnlyCollection<Guid> scopedKitchenBranchIds)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(wasteType) && ProductionWasteTypes.All.Contains(wasteType))
        {
            query = query.Where(w => w.WasteType == wasteType);
        }
        if (kitchenBranchId.HasValue)
        {
            query = query.Where(w => w.KitchenBranchId == kitchenBranchId.Value);
        }
        query = query.Where(w => scopedKitchenBranchIds.Contains(w.KitchenBranchId));
        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(w => w.RecordedAt.Date >= from);
        }
        if (toDate.HasValue)
        {
            var to = toDate.Value.Date;
            query = query.Where(w => w.RecordedAt.Date <= to);
        }
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(w =>
                w.Reason.ToLower().Contains(f)
                || (w.Notes != null && w.Notes.ToLower().Contains(f))
                || w.WasteType.ToLower().Contains(f));
        }

        return query;
    }

    private static IQueryable<AppProductionWaste> ApplySorting(IQueryable<AppProductionWaste> query, string sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
        {
            return query.OrderByDescending(w => w.RecordedAt);
        }

        var parts = sorting.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var field = parts[0].ToLowerInvariant();
        var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return field switch
        {
            "quantity" => descending ? query.OrderByDescending(w => w.Quantity) : query.OrderBy(w => w.Quantity),
            "totalcost" => descending ? query.OrderByDescending(w => w.TotalCost) : query.OrderBy(w => w.TotalCost),
            "wastetype" => descending ? query.OrderByDescending(w => w.WasteType) : query.OrderBy(w => w.WasteType),
            "reason" => descending ? query.OrderByDescending(w => w.Reason) : query.OrderBy(w => w.Reason),
            "recordedat" => descending ? query.OrderByDescending(w => w.RecordedAt) : query.OrderBy(w => w.RecordedAt),
            _ => query.OrderByDescending(w => w.RecordedAt)
        };
    }

    private async Task<List<ProductionWasteListItem>> HydrateListItemsAsync(
        List<WasteProjection> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return new List<ProductionWasteListItem>();
        }

        var branchIds = rows.Select(r => r.KitchenBranchId).Distinct().ToList();
        var productIds = rows.Select(r => r.ProductId).Distinct().ToList();
        var orderIds = rows.Where(r => r.ProductionOrderId.HasValue).Select(r => r.ProductionOrderId!.Value).Distinct().ToList();

        var branches = await _branchRepository.GetListAsync(b => branchIds.Contains(b.Id), cancellationToken: cancellationToken);
        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id), cancellationToken: cancellationToken);
        var dbContext = await GetDbContextAsync();
        var orders = await dbContext.ProductionOrders
            .Where(o => orderIds.Contains(o.Id))
            .Select(o => new { o.Id, o.OrderNumber })
            .ToListAsync(cancellationToken);

        var branchById = branches.ToDictionary(b => b.Id);
        var productById = products.ToDictionary(p => p.Id);
        var orderById = orders.ToDictionary(o => o.Id, o => o.OrderNumber);

        return rows.Select(r =>
        {
            branchById.TryGetValue(r.KitchenBranchId, out var branch);
            productById.TryGetValue(r.ProductId, out var product);
            var orderNumber = r.ProductionOrderId.HasValue && orderById.TryGetValue(r.ProductionOrderId.Value, out var found)
                ? found
                : null;

            return new ProductionWasteListItem
            {
                Id = r.Id,
                ProductionOrderId = r.ProductionOrderId,
                ProductionOrderNumber = orderNumber,
                KitchenBranchId = r.KitchenBranchId,
                KitchenBranchName = branch?.Name ?? r.KitchenBranchId.ToString(),
                ProductId = r.ProductId,
                ProductName = product?.Name ?? r.ProductId.ToString(),
                ProductSku = product?.SKU ?? string.Empty,
                ProductUnit = product?.Unit ?? string.Empty,
                WasteType = r.WasteType,
                Quantity = r.Quantity,
                UnitCost = r.UnitCost,
                TotalCost = r.TotalCost,
                Reason = r.Reason,
                Notes = r.Notes,
                RecordedAt = r.RecordedAt
            };
        }).ToList();
    }

    private sealed class WasteProjection
    {
        public Guid Id { get; set; }
        public Guid? ProductionOrderId { get; set; }
        public Guid KitchenBranchId { get; set; }
        public Guid ProductId { get; set; }
        public string WasteType { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public string Reason { get; set; } = null!;
        public string? Notes { get; set; }
        public DateTime RecordedAt { get; set; }
    }
}
