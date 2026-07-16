using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
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

    public BranchProductionRequestRepository(
        IDbContextProvider<ProductionDbContext> dbContextProvider,
        IRepository<AppBranch, Guid> branchRepository)
        : base(dbContextProvider)
    {
        _branchRepository = branchRepository;
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
                BranchName = branch?.Name ?? h.BranchId.ToString(),
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
