using System;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Entities;
using Inventory.Events;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Intelligence.Services;

public class DecisionMakerService : DomainService
{
    private readonly IRepository<AppInventoryRule, Guid> _rulesRepo;
    private readonly IRepository<AppDecisionLog, Guid> _logsRepo;

    public DecisionMakerService(
        IRepository<AppInventoryRule, Guid> rulesRepo,
        IRepository<AppDecisionLog, Guid> logsRepo)
    {
        _rulesRepo = rulesRepo;
        _logsRepo = logsRepo;
    }

    public async Task EvaluateAsync(StockChangedEto eto)
    {
        var queryable = (await _rulesRepo.GetQueryableAsync())
            .Where(r => r.IsActive
                && (r.ProductId == null || r.ProductId == eto.ProductId)
                && (r.BranchId  == null || r.BranchId  == eto.BranchId))
            .OrderByDescending(r => r.Priority);

        var rules = await AsyncExecuter.ToListAsync(queryable);

        foreach (var rule in rules)
        {
            if (rule.RuleType == "LowStock" && rule.ThresholdValue.HasValue)
            {
                if (eto.NewQty < rule.ThresholdValue.Value)
                {
                    await CreateLogAsync(rule, eto.ProductId, eto.BranchId, "LowStockAlert",
                        $"Stock={eto.NewQty} is below threshold={rule.ThresholdValue} (Rule: '{rule.RuleName}'). Suggested: reorder.",
                        stockAtEval: eto.NewQty);
                }
            }
            else if (rule.RuleType == "ExcessStock" && rule.ThresholdValue.HasValue)
            {
                if (eto.NewQty > rule.ThresholdValue.Value)
                {
                    await CreateLogAsync(rule, eto.ProductId, eto.BranchId, "ExcessStockAlert",
                        $"Stock={eto.NewQty} exceeds threshold={rule.ThresholdValue} (Rule: '{rule.RuleName}'). Suggested: redistribute.",
                        stockAtEval: eto.NewQty);
                }
            }
            // TransferSuggestion and DeadStock are evaluated by a separate background job — not here
        }
    }

    private async Task CreateLogAsync(AppInventoryRule rule, Guid productId, Guid? branchId,
        string decisionType, string reasoning, int? stockAtEval = null, int? daysWithoutSale = null,
        Guid? sourceBranchId = null, Guid? targetBranchId = null)
    {
        var log = new AppDecisionLog(
            GuidGenerator.Create(),
            rule.Id,
            productId,
            branchId,
            decisionType,
            reasoning,
            rule.SuggestedAction,
            stockAtEval,
            daysWithoutSale,
            sourceBranchId,
            targetBranchId
        );
        await _logsRepo.InsertAsync(log);
    }
}
