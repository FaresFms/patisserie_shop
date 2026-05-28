using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Entities;
using Intelligence.Permissions;
using Intelligence.Rules;
using Inventory.Branches;
using Inventory.Products;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Intelligence.Rules;

[Authorize(IntelligencePermissions.Rules.Default)]
public class InventoryRuleAppService : IntelligenceAppService, IInventoryRuleAppService
{
    private readonly IInventoryRuleRepository _ruleRepository;
    private readonly InventoryRuleManager _ruleManager;
    private readonly IProductAppService _productAppService;
    private readonly IBranchAppService _branchAppService;

    public InventoryRuleAppService(
        IInventoryRuleRepository ruleRepository,
        InventoryRuleManager ruleManager,
        IProductAppService productAppService,
        IBranchAppService branchAppService)
    {
        _ruleRepository = ruleRepository;
        _ruleManager = ruleManager;
        _productAppService = productAppService;
        _branchAppService = branchAppService;
    }

    public async Task<InventoryRuleDto> GetAsync(Guid id)
    {
        var rule = await _ruleRepository.GetAsync(id);
        var dto = ObjectMapper.Map<AppInventoryRule, InventoryRuleDto>(rule);
        await ResolveScopeNamesAsync([dto]);
        return dto;
    }

    public async Task<PagedResultDto<InventoryRuleDto>> GetListAsync(GetInventoryRulesInput input)
    {
        var totalCount = await _ruleRepository.CountFilteredAsync(input.Filter, input.RuleType, input.IsActive);

        var items = await _ruleRepository.GetFilteredListAsync(
            input.Filter,
            input.RuleType,
            input.IsActive,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        var dtos = items.ConvertAll(r => ObjectMapper.Map<AppInventoryRule, InventoryRuleDto>(r));
        await ResolveScopeNamesAsync(dtos);

        return new PagedResultDto<InventoryRuleDto>(totalCount, dtos);
    }

    [Authorize(IntelligencePermissions.Rules.Manage)]
    public async Task<InventoryRuleDto> CreateAsync(CreateInventoryRuleDto input)
    {
        var rule = await _ruleManager.CreateAsync(
            input.RuleName,
            input.RuleType,
            input.ProductId,
            input.BranchId,
            input.ThresholdValue,
            input.ThresholdDays,
            input.SuggestedAction,
            input.Priority,
            input.IsActive);

        await _ruleRepository.InsertAsync(rule, autoSave: true);

        var dto = ObjectMapper.Map<AppInventoryRule, InventoryRuleDto>(rule);
        await ResolveScopeNamesAsync([dto]);
        return dto;
    }

    [Authorize(IntelligencePermissions.Rules.Manage)]
    public async Task<InventoryRuleDto> UpdateAsync(Guid id, UpdateInventoryRuleDto input)
    {
        var rule = await _ruleRepository.GetAsync(id);

        rule.UpdateInfo(
            input.RuleName,
            input.RuleType,
            input.ProductId,
            input.BranchId,
            input.ThresholdValue,
            input.ThresholdDays,
            input.SuggestedAction,
            input.Priority,
            input.IsActive);

        await _ruleRepository.UpdateAsync(rule, autoSave: true);

        var dto = ObjectMapper.Map<AppInventoryRule, InventoryRuleDto>(rule);
        await ResolveScopeNamesAsync([dto]);
        return dto;
    }

    [Authorize(IntelligencePermissions.Rules.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        await _ruleRepository.DeleteAsync(id);
    }

    /// <summary>
    /// Fills ProductName / BranchName on the DTOs for the Scope column, reusing the
    /// existing inventory lookup endpoints (no new lookup endpoints are introduced).
    /// </summary>
    private async Task ResolveScopeNamesAsync(IReadOnlyCollection<InventoryRuleDto> dtos)
    {
        if (dtos.Any(d => d.ProductId.HasValue))
        {
            var products = await _productAppService.GetLookupAsync();
            var productNames = products.ToDictionary(p => p.Id, p => $"{p.SKU} — {p.Name}");

            foreach (var dto in dtos.Where(d => d.ProductId.HasValue))
            {
                dto.ProductName = productNames.TryGetValue(dto.ProductId!.Value, out var name) ? name : null;
            }
        }

        if (dtos.Any(d => d.BranchId.HasValue))
        {
            var branches = await _branchAppService.GetLookupAsync();
            var branchNames = branches.ToDictionary(b => b.Id, b => b.Name);

            foreach (var dto in dtos.Where(d => d.BranchId.HasValue))
            {
                dto.BranchName = branchNames.TryGetValue(dto.BranchId!.Value, out var name) ? name : null;
            }
        }
    }
}
