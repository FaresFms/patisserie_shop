using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.StockBatches;
using Microsoft.EntityFrameworkCore;
using Production.BranchRequests;
using Production.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Production.EntityFrameworkCore;

public class BranchProductionRequestRepository
    : EfCoreRepository<ProductionDbContext, AppBranchProductionRequest, Guid>,
      IBranchProductionRequestRepository
{
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppBranchInventory, Guid> _inventoryRepository;
    private readonly IStockBatchRepository _stockBatchRepository;

    public BranchProductionRequestRepository(
        IDbContextProvider<ProductionDbContext> dbContextProvider,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppBranchInventory, Guid> inventoryRepository,
        IStockBatchRepository stockBatchRepository)
        : base(dbContextProvider)
    {
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _inventoryRepository = inventoryRepository;
        _stockBatchRepository = stockBatchRepository;
    }

    public async Task<AppBranchProductionRequest> GetWithItemsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = await WithDetailsAsync(r => r.Items);
        return await query.FirstAsync(r => r.Id == id, GetCancellationToken(cancellationToken));
    }

    public async Task<long> CountFilteredAsync(
        string? filter,
        string? status,
        Guid? branchId,
        IReadOnlyCollection<Guid>? scopedBranchIds,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, status, branchId, scopedBranchIds, cancellationToken);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<BranchProductionRequestListItem>> GetFilteredListAsync(
        string? filter,
        string? status,
        Guid? branchId,
        IReadOnlyCollection<Guid>? scopedBranchIds,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);
        var query = await BuildFilteredQueryAsync(filter, status, branchId, scopedBranchIds, cancellationToken);

        var headers = await query
            .Select(r => new HeaderProjection
            {
                Id = r.Id,
                RequestNumber = r.RequestNumber,
                BranchId = r.BranchId,
                NeededByDate = r.NeededByDate,
                Priority = r.Priority,
                Status = r.Status,
                ItemCount = r.Items.Count,
                RequestedTotalQuantity = r.Items.Sum(i => i.RequestedQuantity),
                ApprovedTotalQuantity = r.Items.Sum(i => i.ApprovedQuantity),
                PlannedTotalQuantity = r.Items.Sum(i => i.PlannedQuantity),
                FulfilledTotalQuantity = r.Items.Sum(i => i.FulfilledQuantity)
            })
            .ToListAsync(ct);

        if (headers.Count == 0)
        {
            return new List<BranchProductionRequestListItem>();
        }

        var branchIds = headers.Select(h => h.BranchId).Distinct().ToList();
        var branches = await _branchRepository.GetListAsync(b => branchIds.Contains(b.Id), cancellationToken: ct);
        var branchById = branches.ToDictionary(b => b.Id);

        var rows = headers.Select(h =>
        {
            branchById.TryGetValue(h.BranchId, out var branch);
            return new BranchProductionRequestListItem
            {
                Id = h.Id,
                RequestNumber = h.RequestNumber,
                BranchId = h.BranchId,
                BranchName = branch?.DisplayName ?? h.BranchId.ToString(),
                NeededByDate = h.NeededByDate,
                Priority = h.Priority,
                Status = h.Status,
                ItemCount = h.ItemCount,
                RequestedTotalQuantity = h.RequestedTotalQuantity,
                ApprovedTotalQuantity = h.ApprovedTotalQuantity,
                PlannedTotalQuantity = h.PlannedTotalQuantity,
                FulfilledTotalQuantity = h.FulfilledTotalQuantity
            };
        });

        return ApplySorting(rows, sorting)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToList();
    }

    public async Task<List<BranchProductionRequestFulfillmentTarget>> GetFulfillmentTargetsAsync(
        Guid branchId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);
        var demandStatuses = new[]
        {
            BranchProductionRequestStatuses.Approved,
            BranchProductionRequestStatuses.PartiallyPlanned,
            BranchProductionRequestStatuses.Planned,
            BranchProductionRequestStatuses.PartiallyFulfilled
        };

        var dbContext = await GetDbContextAsync();
        var requests = await dbContext.BranchProductionRequests
            .Include(r => r.Items)
            .Where(r => r.BranchId == branchId
                && demandStatuses.Contains(r.Status)
                && r.Items.Any(i => i.ProductId == productId && i.ApprovedQuantity > i.FulfilledQuantity))
            .ToListAsync(ct);

        return requests
            .SelectMany(r => r.Items
                .Where(i => i.ProductId == productId && i.ApprovedQuantity > i.FulfilledQuantity)
                .Select(i => new BranchProductionRequestFulfillmentTarget
                {
                    RequestId = r.Id,
                    RequestItemId = i.Id,
                    BranchId = r.BranchId,
                    ProductId = i.ProductId,
                    NeededByDate = r.NeededByDate,
                    RemainingQuantity = i.ApprovedQuantity - i.FulfilledQuantity
                }))
            .OrderBy(x => x.NeededByDate)
            .ThenBy(x => x.RequestId)
            .ToList();
    }

    public async Task<List<BranchProductionRequestPlanningTarget>> GetPlanningTargetsAsync(
        IReadOnlyCollection<Guid> productIds,
        DateTime neededBefore,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new List<BranchProductionRequestPlanningTarget>();
        }

        var demandStatuses = new[]
        {
            BranchProductionRequestStatuses.Approved,
            BranchProductionRequestStatuses.PartiallyPlanned,
            BranchProductionRequestStatuses.Planned,
            BranchProductionRequestStatuses.PartiallyFulfilled
        };

        var dbContext = await GetDbContextAsync();
        var requests = await dbContext.BranchProductionRequests
            .Include(r => r.Items)
            .Where(r => demandStatuses.Contains(r.Status)
                && r.NeededByDate < neededBefore
                && r.Items.Any(i => ids.Contains(i.ProductId) && i.ApprovedQuantity > i.PlannedQuantity))
            .ToListAsync(ct);

        return requests
            .SelectMany(r => r.Items
                .Where(i => ids.Contains(i.ProductId) && i.ApprovedQuantity > i.PlannedQuantity)
                .Select(i => new BranchProductionRequestPlanningTarget
                {
                    RequestId = r.Id,
                    RequestItemId = i.Id,
                    BranchId = r.BranchId,
                    ProductId = i.ProductId,
                    NeededByDate = r.NeededByDate,
                    RemainingUnplannedQuantity = i.ApprovedQuantity - i.PlannedQuantity
                }))
            .OrderBy(x => x.NeededByDate)
            .ThenBy(x => x.RequestId)
            .ThenBy(x => x.RequestItemId)
            .ToList();
    }

    public async Task<List<BranchProductionRequestStockDispatchTarget>> GetStockDispatchTargetsAsync(
        Guid kitchenBranchId,
        DateTime usableOnDate,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);
        var demandStatuses = new[]
        {
            BranchProductionRequestStatuses.Approved,
            BranchProductionRequestStatuses.PartiallyPlanned,
            BranchProductionRequestStatuses.Planned,
            BranchProductionRequestStatuses.PartiallyFulfilled
        };

        var dbContext = await GetDbContextAsync();
        var requests = await dbContext.BranchProductionRequests
            .Include(r => r.Items)
            .Where(r => demandStatuses.Contains(r.Status)
                && r.Items.Any(i => i.ApprovedQuantity > i.PlannedQuantity))
            .ToListAsync(ct);

        var productIds = requests
            .SelectMany(r => r.Items)
            .Where(i => i.ApprovedQuantity > i.PlannedQuantity)
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();
        if (productIds.Count == 0)
        {
            return new List<BranchProductionRequestStockDispatchTarget>();
        }

        var branchIds = requests.Select(r => r.BranchId).Distinct().ToList();
        var branches = await _branchRepository.GetListAsync(b => branchIds.Contains(b.Id), cancellationToken: ct);
        var branchById = branches.ToDictionary(b => b.Id);
        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id), cancellationToken: ct);
        var productById = products.ToDictionary(p => p.Id);
        var inventories = await _inventoryRepository.GetListAsync(
            i => i.BranchId == kitchenBranchId && productIds.Contains(i.ProductId),
            cancellationToken: ct);
        var inventoryByProduct = inventories.ToDictionary(i => i.ProductId);
        var nonExpired = await _stockBatchRepository.GetNonExpiredQuantitiesByProductAsync(
            kitchenBranchId,
            usableOnDate.Date,
            ct);

        var committedOrders = await dbContext.ProductionOrders
            .Include(o => o.Allocations)
            .Where(o => o.KitchenBranchId == kitchenBranchId
                && o.Status == ProductionOrderStatuses.Completed
                && productIds.Contains(o.FinishedProductId))
            .ToListAsync(ct);
        var committedByProduct = committedOrders
            .GroupBy(o => o.FinishedProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(o => o.Allocations.Sum(a =>
                    Math.Max(0, a.AllocatedQuantity - a.DispatchedQuantity))));

        var usableByProduct = new Dictionary<Guid, int>();
        var availablePoolByProduct = new Dictionary<Guid, int>();
        foreach (var productId in productIds)
        {
            productById.TryGetValue(productId, out var product);
            inventoryByProduct.TryGetValue(productId, out var inventory);
            var usable = product == null || inventory == null
                ? 0
                : StockBatchManager.GetUsableQuantity(product, inventory, nonExpired);
            committedByProduct.TryGetValue(productId, out var committed);
            usableByProduct[productId] = usable;
            availablePoolByProduct[productId] = Math.Max(0, usable - committed);
        }

        var rows = new List<BranchProductionRequestStockDispatchTarget>();
        foreach (var request in requests
                     .OrderBy(r => r.NeededByDate)
                     .ThenBy(r => PriorityRank(r.Priority))
                     .ThenBy(r => r.RequestNumber, StringComparer.OrdinalIgnoreCase))
        {
            branchById.TryGetValue(request.BranchId, out var branch);
            foreach (var item in request.Items
                         .Where(i => i.ApprovedQuantity > i.PlannedQuantity)
                         .OrderBy(i => productById.TryGetValue(i.ProductId, out var product)
                             ? product.DisplayName
                             : i.ProductId.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                productById.TryGetValue(item.ProductId, out var product);
                committedByProduct.TryGetValue(item.ProductId, out var committed);
                var freeStock = Math.Max(0, usableByProduct.GetValueOrDefault(item.ProductId) - committed);
                var remainingUnplanned = item.ApprovedQuantity - item.PlannedQuantity;
                var dispatchable = Math.Min(
                    remainingUnplanned,
                    availablePoolByProduct.GetValueOrDefault(item.ProductId));
                availablePoolByProduct[item.ProductId] = Math.Max(
                    0,
                    availablePoolByProduct.GetValueOrDefault(item.ProductId) - dispatchable);

                rows.Add(new BranchProductionRequestStockDispatchTarget
                {
                    RequestId = request.Id,
                    RequestItemId = item.Id,
                    RequestNumber = request.RequestNumber,
                    BranchId = request.BranchId,
                    BranchName = branch?.DisplayName ?? request.BranchId.ToString(),
                    NeededByDate = request.NeededByDate,
                    Priority = request.Priority,
                    ProductId = item.ProductId,
                    ProductName = product?.DisplayName ?? item.ProductId.ToString(),
                    ProductSku = product?.SKU ?? string.Empty,
                    Unit = product?.DisplayUnit ?? string.Empty,
                    ApprovedQuantity = item.ApprovedQuantity,
                    PlannedQuantity = item.PlannedQuantity,
                    FulfilledQuantity = item.FulfilledQuantity,
                    RemainingUnplannedQuantity = remainingUnplanned,
                    UsableKitchenStock = usableByProduct.GetValueOrDefault(item.ProductId),
                    CommittedKitchenStock = committed,
                    AvailableUncommittedKitchenStock = freeStock,
                    DispatchableQuantity = dispatchable
                });
            }
        }

        if (string.IsNullOrWhiteSpace(filter))
        {
            return rows;
        }

        var normalized = filter.Trim();
        return rows.Where(row =>
                row.RequestNumber.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || row.BranchName.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || row.ProductName.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || row.ProductSku.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<BranchProductionRequestStockDispatchTarget?> FindStockDispatchTargetAsync(
        Guid kitchenBranchId,
        Guid requestId,
        Guid requestItemId,
        DateTime usableOnDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await GetStockDispatchTargetsAsync(
            kitchenBranchId,
            usableOnDate,
            cancellationToken: cancellationToken);
        return rows.FirstOrDefault(row => row.RequestId == requestId && row.RequestItemId == requestItemId);
    }

    private static int PriorityRank(string priority) => priority switch
    {
        ProductionPriorities.Urgent => 0,
        ProductionPriorities.Normal => 1,
        _ => 2
    };

    private async Task<IQueryable<AppBranchProductionRequest>> BuildFilteredQueryAsync(
        string? filter,
        string? status,
        Guid? branchId,
        IReadOnlyCollection<Guid>? scopedBranchIds,
        CancellationToken cancellationToken)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(r => r.RequestNumber.ToLower().Contains(f));
        }

        if (!string.IsNullOrWhiteSpace(status) && BranchProductionRequestStatuses.All.Contains(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (branchId.HasValue)
        {
            query = query.Where(r => r.BranchId == branchId.Value);
        }

        if (scopedBranchIds != null)
        {
            var ids = scopedBranchIds.ToList();
            query = query.Where(r => ids.Contains(r.BranchId));
        }

        return query;
    }

    private static IEnumerable<BranchProductionRequestListItem> ApplySorting(
        IEnumerable<BranchProductionRequestListItem> rows,
        string sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
        {
            return rows
                .OrderByDescending(r => r.NeededByDate)
                .ThenBy(r => r.BranchName, StringComparer.OrdinalIgnoreCase);
        }

        var parts = sorting.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var field = parts[0];
        var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        Func<BranchProductionRequestListItem, object?> keySelector = field.ToLowerInvariant() switch
        {
            "requestnumber" => r => r.RequestNumber,
            "branchname" => r => r.BranchName,
            "neededbydate" => r => r.NeededByDate,
            "priority" => r => r.Priority,
            "status" => r => r.Status,
            "requestedtotalquantity" => r => r.RequestedTotalQuantity,
            "approvedtotalquantity" => r => r.ApprovedTotalQuantity,
            _ => r => r.NeededByDate
        };

        return descending ? rows.OrderByDescending(keySelector) : rows.OrderBy(keySelector);
    }

    private sealed class HeaderProjection
    {
        public Guid Id { get; set; }
        public string RequestNumber { get; set; } = null!;
        public Guid BranchId { get; set; }
        public DateTime NeededByDate { get; set; }
        public string Priority { get; set; } = null!;
        public string Status { get; set; } = null!;
        public int ItemCount { get; set; }
        public int RequestedTotalQuantity { get; set; }
        public int ApprovedTotalQuantity { get; set; }
        public int PlannedTotalQuantity { get; set; }
        public int FulfilledTotalQuantity { get; set; }
    }
}
