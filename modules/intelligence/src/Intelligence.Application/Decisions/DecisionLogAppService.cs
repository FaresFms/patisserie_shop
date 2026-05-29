using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Intelligence.Entities;
using Intelligence.Permissions;
using Inventory.Branches;
using Inventory.BranchInventory;
using Inventory.Permissions;
using Inventory.Products;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Users;

namespace Intelligence.Decisions;

[Authorize(IntelligencePermissions.DecisionLogs.Default)]
public class DecisionLogAppService : IntelligenceAppService, IDecisionLogAppService
{
    private readonly IDecisionLogRepository _decisionLogRepository;
    private readonly IProductAppService _productAppService;
    private readonly IBranchAppService _branchAppService;
    private readonly IBranchInventoryAppService _branchInventoryAppService;
    private readonly ICurrentUser _currentUser;
    private readonly IAuthorizationService _authorizationService;

    public DecisionLogAppService(
        IDecisionLogRepository decisionLogRepository,
        IProductAppService productAppService,
        IBranchAppService branchAppService,
        IBranchInventoryAppService branchInventoryAppService,
        ICurrentUser currentUser,
        IAuthorizationService authorizationService)
    {
        _decisionLogRepository = decisionLogRepository;
        _productAppService = productAppService;
        _branchAppService = branchAppService;
        _branchInventoryAppService = branchInventoryAppService;
        _currentUser = currentUser;
        _authorizationService = authorizationService;
    }

    public async Task<PagedResultDto<DecisionLogDto>> GetListAsync(GetDecisionLogsInput input)
    {
        var scope = await GetBranchScopeAsync();

        var totalCount = await _decisionLogRepository.CountFilteredAsync(
            input.Filter, input.DecisionType, input.Status, input.BranchId, input.ProductId,
            input.FromDate, input.ToDate, scope);

        var items = await _decisionLogRepository.GetFilteredListAsync(
            input.Filter, input.DecisionType, input.Status, input.BranchId, input.ProductId,
            input.FromDate, input.ToDate, scope,
            input.Sorting ?? string.Empty, input.SkipCount, input.MaxResultCount);

        var dtos = items.ConvertAll(BuildDto);
        await ResolveLookupNamesAsync(dtos);

        return new PagedResultDto<DecisionLogDto>(totalCount, dtos);
    }

    public async Task<DecisionLogSummaryDto> GetSummaryAsync()
    {
        var scope = await GetBranchScopeAsync();
        var s = await _decisionLogRepository.GetSummaryAsync(scope);

        return new DecisionLogSummaryDto
        {
            TotalPending = s.TotalPending,
            LowStockPending = s.LowStockPending,
            ExcessStockPending = s.ExcessStockPending,
            ResolvedToday = s.ResolvedToday
        };
    }

    [Authorize(IntelligencePermissions.DecisionLogs.Acknowledge)]
    public Task<DecisionLogDto> AcknowledgeAsync(Guid id)
        => MutateAsync(id, (log, userId) => log.Acknowledge(userId));

    [Authorize(IntelligencePermissions.DecisionLogs.Acknowledge)]
    public Task<DecisionLogDto> DismissAsync(Guid id)
        => MutateAsync(id, (log, userId) => log.Dismiss(userId));

    [Authorize(IntelligencePermissions.DecisionLogs.Acknowledge)]
    public Task<DecisionLogDto> ExecuteAsync(Guid id)
        => MutateAsync(id, (log, userId) => log.MarkExecuted(userId));

    private async Task<DecisionLogDto> MutateAsync(Guid id, Action<AppDecisionLog, Guid> transition)
    {
        var userId = _currentUser.Id
            ?? throw new AbpAuthorizationException("Authenticated user required for this action.");

        var log = await _decisionLogRepository.GetAsync(id);

        // Enforce scope on the mutating user too: branch managers can't acknowledge
        // logs outside their accessible set.
        var scope = await GetBranchScopeAsync();
        if (scope != null && log.BranchId.HasValue && !scope.Contains(log.BranchId.Value))
        {
            throw new AbpAuthorizationException("You do not have access to this decision log's branch.");
        }

        transition(log, userId);

        await _decisionLogRepository.UpdateAsync(log, autoSave: true);

        var joined = await _decisionLogRepository.GetByIdWithRuleNameAsync(id);
        var dto = BuildDto(joined ?? new DecisionLogWithRuleName { DecisionLog = log, RuleName = null });
        await ResolveLookupNamesAsync(new[] { dto });
        return dto;
    }

    private DecisionLogDto BuildDto(DecisionLogWithRuleName joined)
    {
        var dto = ObjectMapper.Map<AppDecisionLog, DecisionLogDto>(joined.DecisionLog);
        dto.RuleName = joined.RuleName;
        return dto;
    }

    /// <summary>
    /// Fills RuleName (already on the DTO from the join), ProductName, BranchName,
    /// SourceBranchName, and TargetBranchName by batching one product-lookup and one
    /// branch-lookup call. Reuses the existing inventory lookup endpoints.
    /// </summary>
    private async Task ResolveLookupNamesAsync(IReadOnlyCollection<DecisionLogDto> dtos)
    {
        if (dtos.Count == 0) return;

        if (dtos.Any(d => d.ProductId != Guid.Empty))
        {
            var products = await _productAppService.GetLookupAsync();
            var productNames = products.ToDictionary(p => p.Id, p => $"{p.SKU} — {p.Name}");
            foreach (var dto in dtos)
            {
                if (productNames.TryGetValue(dto.ProductId, out var name))
                {
                    dto.ProductName = name;
                }
            }
        }

        if (dtos.Any(d => d.BranchId.HasValue || d.SourceBranchId.HasValue || d.TargetBranchId.HasValue))
        {
            var branches = await _branchAppService.GetLookupAsync();
            var branchNames = branches.ToDictionary(b => b.Id, b => b.Name);

            foreach (var dto in dtos)
            {
                if (dto.BranchId.HasValue && branchNames.TryGetValue(dto.BranchId.Value, out var n1))
                    dto.BranchName = n1;
                if (dto.SourceBranchId.HasValue && branchNames.TryGetValue(dto.SourceBranchId.Value, out var n2))
                    dto.SourceBranchName = n2;
                if (dto.TargetBranchId.HasValue && branchNames.TryGetValue(dto.TargetBranchId.Value, out var n3))
                    dto.TargetBranchName = n3;
            }
        }
    }

    /// <summary>
    /// Returns the branch-ID set the current user is allowed to see. Null means
    /// "no scope" (admin / ManageAll): the repository skips the branch filter
    /// entirely. Otherwise the repository restricts to these IDs (plus globally
    /// scoped decisions with BranchId IS NULL).
    /// </summary>
    private async Task<IReadOnlyCollection<Guid>?> GetBranchScopeAsync()
    {
        if (await _authorizationService.IsGrantedAsync(InventoryPermissions.BranchInventory.ManageAll))
        {
            return null;
        }

        var ids = await _branchInventoryAppService.GetAccessibleBranchIdsAsync();
        return ids;
    }
}
