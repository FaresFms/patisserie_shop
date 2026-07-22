using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Stocktakes;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Inventory.EntityFrameworkCore;

public class StocktakeSessionRepository
    : EfCoreRepository<InventoryDbContext, AppStocktakeSession, Guid>,
      IStocktakeSessionRepository
{
    public StocktakeSessionRepository(IDbContextProvider<InventoryDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<AppStocktakeSession?> FindOpenByBranchAsync(
        Guid branchId,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return await dbContext.Set<AppStocktakeSession>()
            .Include(session => session.Lines)
            .Where(session => session.BranchId == branchId
                && (session.Status == StocktakeSessionStatuses.Draft
                    || session.Status == StocktakeSessionStatuses.PendingReview))
            .OrderByDescending(session => session.SnapshotAt)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<AppStocktakeSession> GetWithLinesAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var session = await dbContext.Set<AppStocktakeSession>()
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.Id == id, GetCancellationToken(cancellationToken));

        return session ?? throw new EntityNotFoundException(typeof(AppStocktakeSession), id);
    }

    public async Task<long> CountFilteredAsync(
        Guid branchId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(branchId, status);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<AppStocktakeSession>> GetFilteredListAsync(
        Guid branchId,
        string? status,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(branchId, status);
        query = ResolveSorting(sorting) switch
        {
            "SnapshotAt" => query.OrderBy(item => item.SnapshotAt),
            "Status" => query.OrderBy(item => item.Status).ThenByDescending(item => item.SnapshotAt),
            "Status desc" => query.OrderByDescending(item => item.Status).ThenByDescending(item => item.SnapshotAt),
            "ClosedAt" => query.OrderBy(item => item.ClosedAt),
            "ClosedAt desc" => query.OrderByDescending(item => item.ClosedAt),
            _ => query.OrderByDescending(item => item.SnapshotAt)
        };

        return await query
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<StocktakeReconciliationSummary> GetReconciliationSummaryAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default)
    {
        var sessions = await BuildCompletedScopeAsync(fromInclusive, toExclusive, branchIdScope);
        var summary = await sessions
            .GroupBy(_ => 1)
            .Select(group => new StocktakeReconciliationSummary
            {
                CompletedSessionCount = group.Count(),
                CountedLineCount = group.Sum(session => session.CountedLineCount),
                DifferenceLineCount = group.Sum(session => session.DifferenceLineCount),
                WriteOffLineCount = group.Sum(session => session.WriteOffLineCount),
                ManualAdjustmentLineCount = group.Sum(session => session.ManualAdjustmentLineCount),
                ShortageQuantity = group.Sum(session => session.ShortageQuantity),
                OverageQuantity = group.Sum(session => session.OverageQuantity)
            })
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));

        return summary ?? new StocktakeReconciliationSummary();
    }

    public async Task<List<StocktakeProductVarianceAggregate>> GetProductVarianceAggregatesAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default)
    {
        var sessions = await BuildCompletedScopeAsync(fromInclusive, toExclusive, branchIdScope);
        var lines =
            from session in sessions
            from line in session.Lines
            where line.CountedQuantity.HasValue && line.CountedQuantity.Value != line.ExpectedQuantity
            select new
            {
                SessionId = session.Id,
                line.ProductId,
                Difference = line.CountedQuantity!.Value - line.ExpectedQuantity,
                line.Reason
            };

        return await lines
            .GroupBy(line => line.ProductId)
            .Select(group => new StocktakeProductVarianceAggregate
            {
                ProductId = group.Key,
                SessionCount = group.Select(line => line.SessionId).Distinct().Count(),
                DifferenceLineCount = group.Count(),
                ShortageQuantity = group.Sum(line => line.Difference < 0 ? -line.Difference : 0),
                OverageQuantity = group.Sum(line => line.Difference > 0 ? line.Difference : 0),
                WriteOffQuantity = group.Sum(line =>
                    line.Difference < 0
                    && (line.Reason == StocktakeVarianceReasons.Damaged
                        || line.Reason == StocktakeVarianceReasons.Expired
                        || line.Reason == StocktakeVarianceReasons.Waste)
                        ? -line.Difference
                        : 0),
                UnrecordedSaleQuantity = group.Sum(line =>
                    line.Difference < 0 && line.Reason == StocktakeVarianceReasons.UnrecordedSale
                        ? -line.Difference
                        : 0)
            })
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<StocktakeReasonAggregate>> GetReasonAggregatesAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default)
    {
        var sessions = await BuildCompletedScopeAsync(fromInclusive, toExclusive, branchIdScope);
        var lines =
            from session in sessions
            from line in session.Lines
            where line.CountedQuantity.HasValue && line.CountedQuantity.Value != line.ExpectedQuantity
            select new
            {
                Reason = line.Reason ?? StocktakeVarianceReasons.Other,
                Difference = line.CountedQuantity!.Value - line.ExpectedQuantity
            };

        return await lines
            .GroupBy(line => line.Reason)
            .Select(group => new StocktakeReasonAggregate
            {
                Reason = group.Key,
                LineCount = group.Count(),
                ShortageQuantity = group.Sum(line => line.Difference < 0 ? -line.Difference : 0),
                OverageQuantity = group.Sum(line => line.Difference > 0 ? line.Difference : 0)
            })
            .OrderByDescending(item => item.ShortageQuantity + item.OverageQuantity)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<AppStocktakeSession>> BuildFilteredQueryAsync(Guid branchId, string? status)
    {
        var dbContext = await GetDbContextAsync();
        var query = dbContext.Set<AppStocktakeSession>()
            .AsNoTracking()
            .Where(item => item.BranchId == branchId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status == status);
        }

        return query;
    }

    private async Task<IQueryable<AppStocktakeSession>> BuildCompletedScopeAsync(
        DateTime fromInclusive,
        DateTime toExclusive,
        IReadOnlyCollection<Guid>? branchIdScope)
    {
        var dbContext = await GetDbContextAsync();
        var query = dbContext.Set<AppStocktakeSession>()
            .AsNoTracking()
            .Where(session => session.Status == StocktakeSessionStatuses.Completed
                && session.ClosedAt.HasValue
                && session.ClosedAt.Value >= fromInclusive
                && session.ClosedAt.Value < toExclusive);

        if (branchIdScope is not null)
        {
            query = query.Where(session => branchIdScope.Contains(session.BranchId));
        }

        return query;
    }

    private static string ResolveSorting(string? sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting)) return "SnapshotAt desc";
        return sorting.Trim() switch
        {
            "SnapshotAt" => "SnapshotAt",
            "SnapshotAt desc" => "SnapshotAt desc",
            "Status" => "Status",
            "Status desc" => "Status desc",
            "ClosedAt" => "ClosedAt",
            "ClosedAt desc" => "ClosedAt desc",
            _ => "SnapshotAt desc"
        };
    }
}
