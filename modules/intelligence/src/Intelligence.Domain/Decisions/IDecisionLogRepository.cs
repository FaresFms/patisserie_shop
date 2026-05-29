using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Intelligence.Entities;
using Volo.Abp.Domain.Repositories;

namespace Intelligence.Decisions;

public interface IDecisionLogRepository : IRepository<AppDecisionLog, Guid>
{
    Task<long> CountFilteredAsync(
        string? filter,
        string? decisionType,
        string? status,
        Guid? branchId,
        Guid? productId,
        DateTime? fromDate,
        DateTime? toDate,
        IReadOnlyCollection<Guid>? scopedBranchIds,
        CancellationToken cancellationToken = default);

    Task<List<DecisionLogWithRuleName>> GetFilteredListAsync(
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
        CancellationToken cancellationToken = default);

    Task<DecisionLogWithRuleName?> GetByIdWithRuleNameAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<DecisionLogSummary> GetSummaryAsync(
        IReadOnlyCollection<Guid>? scopedBranchIds,
        CancellationToken cancellationToken = default);
}
