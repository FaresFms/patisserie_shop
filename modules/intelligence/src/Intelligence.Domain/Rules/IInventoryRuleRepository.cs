using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Intelligence.Entities;
using Volo.Abp.Domain.Repositories;

namespace Intelligence.Rules;

public interface IInventoryRuleRepository : IRepository<AppInventoryRule, Guid>
{
    Task<long> CountFilteredAsync(
        string? filter,
        string? ruleType,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<List<AppInventoryRule>> GetFilteredListAsync(
        string? filter,
        string? ruleType,
        bool? isActive,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);
}
