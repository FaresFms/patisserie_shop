using System;
using System.Threading.Tasks;
using Intelligence.Entities;
using Volo.Abp.Domain.Services;

namespace Intelligence.Rules;

/// <summary>
/// Factory/domain-service for AppInventoryRule. The entity constructor is internal,
/// so the aggregate is only ever created through here. The threshold invariant is
/// enforced inside the entity itself.
/// </summary>
public class InventoryRuleManager : DomainService
{
    public Task<AppInventoryRule> CreateAsync(
        string ruleName,
        string ruleType,
        Guid? productId,
        Guid? branchId,
        int? thresholdValue,
        int? thresholdDays,
        string? suggestedAction,
        int priority,
        bool isActive)
    {
        var rule = new AppInventoryRule(
            GuidGenerator.Create(),
            ruleName,
            ruleType,
            productId,
            branchId,
            thresholdValue,
            thresholdDays,
            suggestedAction,
            priority,
            isActive);

        return Task.FromResult(rule);
    }
}
