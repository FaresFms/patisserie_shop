using System;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.Entities;
using Production.Entities;
using Production.Orders;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Production.Plans;

public class ProductionPlanManager : DomainService
{
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IProductionPlanRepository _planRepository;
    private readonly IProductionOrderRepository _orderRepository;

    public ProductionPlanManager(
        IRepository<AppBranch, Guid> branchRepository,
        IProductionPlanRepository planRepository,
        IProductionOrderRepository orderRepository)
    {
        _branchRepository = branchRepository;
        _planRepository = planRepository;
        _orderRepository = orderRepository;
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

    public async Task CancelAsync(AppProductionPlan plan)
    {
        Check.NotNull(plan, nameof(plan));
        var hasOrders = await _orderRepository.HasOrdersForPlanAsync(plan.Id);
        plan.Cancel(hasOrders);
    }

    public async Task RefreshLifecycleAsync(AppProductionPlan plan)
    {
        Check.NotNull(plan, nameof(plan));
        if (plan.Status == ProductionPlanStatuses.Draft
            || plan.Status == ProductionPlanStatuses.Cancelled
            || plan.Status == ProductionPlanStatuses.Closed)
        {
            return;
        }

        var statuses = await _orderRepository.GetStatusesForPlanAsync(plan.Id);
        if (statuses.Count == 0)
        {
            return;
        }

        if (statuses.All(x => x == ProductionOrderStatuses.Completed || x == ProductionOrderStatuses.Cancelled))
        {
            plan.Close();
            return;
        }

        if (statuses.Any(x => x == ProductionOrderStatuses.InProduction
            || x == ProductionOrderStatuses.Completed))
        {
            plan.MarkInProgress();
        }
    }

    private static string CreatePlanNumber(DateTime productionDate) =>
        $"PLAN-{productionDate:yyyyMMdd}-{DateTime.UtcNow:HHmmssfff}";
}
