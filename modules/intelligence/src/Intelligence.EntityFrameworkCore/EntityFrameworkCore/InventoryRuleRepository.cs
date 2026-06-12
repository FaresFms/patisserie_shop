using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Intelligence.Entities;
using Intelligence.Rules;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Intelligence.EntityFrameworkCore;

public class InventoryRuleRepository
    : EfCoreRepository<IntelligenceDbContext, AppInventoryRule, Guid>,
      IInventoryRuleRepository
{
    public InventoryRuleRepository(IDbContextProvider<IntelligenceDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<long> CountFilteredAsync(
        string? filter,
        string? ruleType,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, ruleType, isActive);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<AppInventoryRule>> GetFilteredListAsync(
        string? filter,
        string? ruleType,
        bool? isActive,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, ruleType, isActive);

        var ordered = query
            .OrderBy(ResolveSorting(sorting))
            .Skip(skipCount)
            .Take(maxResultCount);

        return await ordered.ToListAsync(GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<AppInventoryRule>> BuildFilteredQueryAsync(
        string? filter,
        string? ruleType,
        bool? isActive)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(r => r.RuleName.ToLower().Contains(f));
        }

        if (!string.IsNullOrWhiteSpace(ruleType))
        {
            query = query.Where(r => r.RuleType == ruleType);
        }

        if (isActive.HasValue)
        {
            query = query.Where(r => r.IsActive == isActive.Value);
        }

        return query;
    }

    private static string ResolveSorting(string? sorting)
        => string.IsNullOrWhiteSpace(sorting)
            ? $"{nameof(AppInventoryRule.Priority)} desc"
            : sorting.Trim();
}
