using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Intelligence.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Intelligence.EntityFrameworkCore;

public class DecisionLogRepository
    : EfCoreRepository<IntelligenceDbContext, AppDecisionLog, Guid>,
      IDecisionLogRepository
{
    public DecisionLogRepository(IDbContextProvider<IntelligenceDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<long> CountFilteredAsync(
        string? filter,
        string? decisionType,
        string? status,
        Guid? branchId,
        Guid? productId,
        DateTime? fromDate,
        DateTime? toDate,
        IReadOnlyCollection<Guid>? scopedBranchIds,
        IReadOnlyCollection<string>? statusIn = null,
        IReadOnlyCollection<string>? decisionTypeIn = null,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, decisionType, status, branchId, productId,
            fromDate, toDate, scopedBranchIds, statusIn, decisionTypeIn);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<DecisionLogWithRuleName>> GetFilteredListAsync(
        string? filter,
        string? decisionType,
        string? status,
        Guid? branchId,
        Guid? productId,
        DateTime? fromDate,
        DateTime? toDate,
        IReadOnlyCollection<Guid>? scopedBranchIds,
        string sorting,
        int skipCount,
        int maxResultCount,
        IReadOnlyCollection<string>? statusIn = null,
        IReadOnlyCollection<string>? decisionTypeIn = null,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, decisionType, status, branchId, productId,
            fromDate, toDate, scopedBranchIds, statusIn, decisionTypeIn);

        var ordered = query
            .OrderBy(ResolveSorting(sorting))
            .Skip(skipCount)
            .Take(maxResultCount);

        var rules = (await GetDbContextAsync()).Set<AppInventoryRule>();

        var joined = from log in ordered
                     join rule in rules on log.RuleId equals rule.Id into rj
                     from rule in rj.DefaultIfEmpty()
                     select new DecisionLogWithRuleName
                     {
                         DecisionLog = log,
                         RuleName = rule != null ? rule.RuleName : null
                     };

        return await joined.ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<DecisionLogWithRuleName?> GetByIdWithRuleNameAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var logs = await GetQueryableAsync();
        var rules = (await GetDbContextAsync()).Set<AppInventoryRule>();

        var joined = from log in logs.Where(l => l.Id == id)
                     join rule in rules on log.RuleId equals rule.Id into rj
                     from rule in rj.DefaultIfEmpty()
                     select new DecisionLogWithRuleName
                     {
                         DecisionLog = log,
                         RuleName = rule != null ? rule.RuleName : null
                     };

        return await joined.FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<DecisionLogSummary> GetSummaryAsync(
        IReadOnlyCollection<Guid>? scopedBranchIds,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        query = ApplyBranchScope(query, scopedBranchIds);

        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);
        var pending = DecisionLogStatuses.Pending;
        var lowStockAlert = DecisionTypes.LowStockAlert;
        var excessStockAlert = DecisionTypes.ExcessStockAlert;

        // Single round-trip via a grouped aggregate; falls back to four scalars if
        // the provider rejects the grouping.
        var totalPending = await query
            .CountAsync(l => l.Status == pending, GetCancellationToken(cancellationToken));

        var lowPending = await query
            .CountAsync(l => l.Status == pending && l.DecisionType == lowStockAlert,
                GetCancellationToken(cancellationToken));

        var excessPending = await query
            .CountAsync(l => l.Status == pending && l.DecisionType == excessStockAlert,
                GetCancellationToken(cancellationToken));

        var resolvedToday = await query
            .CountAsync(l => l.Status != pending
                          && l.CreationTime >= todayUtc
                          && l.CreationTime < tomorrowUtc,
                GetCancellationToken(cancellationToken));

        return new DecisionLogSummary
        {
            TotalPending = totalPending,
            LowStockPending = lowPending,
            ExcessStockPending = excessPending,
            ResolvedToday = resolvedToday
        };
    }

    public async Task<List<RuleEffectivenessRow>> GetRuleEffectivenessAsync(
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();

        // Local copies so EF translates the constants into parameters.
        var pending = DecisionLogStatuses.Pending;
        var acknowledged = DecisionLogStatuses.Acknowledged;
        var dismissed = DecisionLogStatuses.Dismissed;
        var executed = DecisionLogStatuses.Executed;
        var resolved = DecisionOutcomes.Resolved;
        var stockedOut = DecisionOutcomes.StockedOut;

        // One grouped query — Npgsql translates the conditional counts to
        // COUNT(*) FILTER (WHERE ...) aggregates.
        var grouped = query
            .GroupBy(l => l.RuleId)
            .Select(g => new RuleEffectivenessRow
            {
                RuleId = g.Key,
                TotalDecisions = g.Count(),
                Pending = g.Count(l => l.Status == pending),
                Acknowledged = g.Count(l => l.Status == acknowledged),
                Dismissed = g.Count(l => l.Status == dismissed),
                Executed = g.Count(l => l.Status == executed),
                Resolved = g.Count(l => l.Outcome == resolved),
                StockedOut = g.Count(l => l.Outcome == stockedOut),
                DismissedThenStockedOut = g.Count(l => l.Status == dismissed && l.Outcome == stockedOut)
            });

        return await grouped.ToListAsync(GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<AppDecisionLog>> BuildFilteredQueryAsync(
        string? filter,
        string? decisionType,
        string? status,
        Guid? branchId,
        Guid? productId,
        DateTime? fromDate,
        DateTime? toDate,
        IReadOnlyCollection<Guid>? scopedBranchIds,
        IReadOnlyCollection<string>? statusIn = null,
        IReadOnlyCollection<string>? decisionTypeIn = null)
    {
        var query = await GetQueryableAsync();
        query = ApplyBranchScope(query, scopedBranchIds);

        if (statusIn is { Count: > 0 })
        {
            query = query.Where(l => statusIn.Contains(l.Status));
        }

        if (decisionTypeIn is { Count: > 0 })
        {
            query = query.Where(l => decisionTypeIn.Contains(l.DecisionType));
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(l =>
                l.Reasoning.ToLower().Contains(f)
                || (l.SuggestedAction != null && l.SuggestedAction.ToLower().Contains(f)));
        }

        if (!string.IsNullOrWhiteSpace(decisionType))
        {
            query = query.Where(l => l.DecisionType == decisionType);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (branchId.HasValue)
        {
            query = query.Where(l => l.BranchId == branchId.Value);
        }

        if (productId.HasValue)
        {
            query = query.Where(l => l.ProductId == productId.Value);
        }

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(l => l.CreationTime >= from);
        }

        if (toDate.HasValue)
        {
            var toExclusive = toDate.Value.Date.AddDays(1);
            query = query.Where(l => l.CreationTime < toExclusive);
        }

        return query;
    }

    /// <summary>
    /// scopedBranchIds == null → no scope (admin / ManageAll). Non-null collection →
    /// restrict to those branches or include global (BranchId IS NULL) decisions.
    /// </summary>
    private static IQueryable<AppDecisionLog> ApplyBranchScope(
        IQueryable<AppDecisionLog> query,
        IReadOnlyCollection<Guid>? scopedBranchIds)
    {
        if (scopedBranchIds == null) return query;
        if (scopedBranchIds.Count == 0) return query.Where(l => l.BranchId == null);

        var ids = scopedBranchIds.ToArray();
        return query.Where(l => l.BranchId == null || ids.Contains(l.BranchId.Value));
    }

    private static string ResolveSorting(string? sorting)
        => string.IsNullOrWhiteSpace(sorting)
            ? $"{nameof(AppDecisionLog.CreationTime)} desc"
            : sorting.Trim();
}
