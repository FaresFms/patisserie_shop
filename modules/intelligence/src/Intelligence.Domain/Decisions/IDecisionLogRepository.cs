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
        IReadOnlyCollection<string>? statusIn = null,
        IReadOnlyCollection<string>? decisionTypeIn = null,
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
        IReadOnlyCollection<string>? statusIn = null,
        IReadOnlyCollection<string>? decisionTypeIn = null,
        CancellationToken cancellationToken = default);

    Task<DecisionLogWithRuleName?> GetByIdWithRuleNameAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<DecisionLogSummary> GetSummaryAsync(
        IReadOnlyCollection<Guid>? scopedBranchIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Per-rule effectiveness aggregate over ALL decision logs (one grouped query):
    /// workflow status counts plus 48h outcome counts (see <see cref="RuleEffectivenessRow"/>).
    /// </summary>
    Task<List<RuleEffectivenessRow>> GetRuleEffectivenessAsync(
        CancellationToken cancellationToken = default);
}
