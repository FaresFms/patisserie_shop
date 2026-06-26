using System;
using System.Threading.Tasks;
using Inventory;
using Inventory.Entities;
using Production.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Production.Plans;

public class ProductionPlanManager : DomainService
{
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IProductionPlanRepository _planRepository;

    public ProductionPlanManager(
        IRepository<AppBranch, Guid> branchRepository,
        IProductionPlanRepository planRepository)
    {
        _branchRepository = branchRepository;
        _planRepository = planRepository;
    }

    public async Task<AppProductionPlan> CreateDraftAsync(
        Guid kitchenBranchId,
        DateTime productionDate,
        Guid? createdByUserId,
        string? notes)
    {
        await EnsureMainKitchenAsync(kitchenBranchId);

        var plan = new AppProductionPlan(
            GuidGenerator.Create(),
            CreatePlanNumber(productionDate),
            kitchenBranchId,
            productionDate,
            createdByUserId,
            notes);

        var suggestions = await _planRepository.BuildSuggestionsAsync(kitchenBranchId, productionDate);
        foreach (var suggestion in suggestions)
        {
            plan.AddLine(
                GuidGenerator.Create(),
                suggestion.ProductId,
                suggestion.RequestedQuantity,
                suggestion.ForecastQuantity,
                suggestion.CurrentKitchenStock,
                suggestion.SuggestedQuantity,
                suggestion.SuggestedQuantity,
                suggestion.EstimatedIngredientCost,
                suggestion.EstimatedLaborCost,
                suggestion.EstimatedOverheadCost,
                suggestion.EstimatedTotalCost);
        }

        return plan;
    }

    public async Task EnsureMainKitchenAsync(Guid kitchenBranchId)
    {
        var branch = await _branchRepository.GetAsync(kitchenBranchId);
        if (branch.BranchType != BranchTypes.MainKitchen)
        {
            throw new BusinessException(ProductionErrorCodes.KitchenBranchMustBeMainKitchen)
                .WithData("BranchId", kitchenBranchId)
                .WithData("BranchType", branch.BranchType);
        }
    }

    private static string CreatePlanNumber(DateTime productionDate) =>
        $"PLAN-{productionDate:yyyyMMdd}-{DateTime.UtcNow:HHmmssfff}";
}
