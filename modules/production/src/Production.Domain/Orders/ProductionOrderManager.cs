using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.StockBatches;
using Production.BranchRequests;
using Production.Costing;
using Production.Entities;
using Production.Formulas;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Production.Orders;

public class ProductionOrderManager : DomainService
{
    private readonly IProductionOrderRepository _orderRepository;
    private readonly IProductionFormulaRepository _formulaRepository;
    private readonly IRepository<AppBranchInventory, Guid> _inventoryRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IStockBatchRepository _stockBatchRepository;
    private readonly IBranchProductionRequestRepository _requestRepository;

    public ProductionOrderManager(
        IProductionOrderRepository orderRepository,
        IProductionFormulaRepository formulaRepository,
        IRepository<AppBranchInventory, Guid> inventoryRepository,
        IRepository<AppProduct, Guid> productRepository,
        IStockBatchRepository stockBatchRepository,
        IBranchProductionRequestRepository requestRepository)
    {
        _orderRepository = orderRepository;
        _formulaRepository = formulaRepository;
        _inventoryRepository = inventoryRepository;
        _productRepository = productRepository;
        _stockBatchRepository = stockBatchRepository;
        _requestRepository = requestRepository;
    }

    public async Task<List<AppProductionOrder>> CreateFromPlanAsync(
        AppProductionPlan plan,
        Guid? createdByUserId)
    {
        Check.NotNull(plan, nameof(plan));

        if (plan.Status != ProductionPlanStatuses.Confirmed)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidPlanStatusTransition)
                .WithData("CurrentStatus", plan.Status)
                .WithData("TargetStatus", ProductionPlanStatuses.Confirmed);
        }

        var result = new List<AppProductionOrder>();
        var requestsById = new Dictionary<Guid, AppBranchProductionRequest>();
        var sequence = 1;
        var productIds = plan.Lines
            .Where(x => x.PlannedQuantity > 0)
            .Select(x => x.ProductId)
            .Distinct()
            .ToList();
        var planningTargets = await _requestRepository.GetPlanningTargetsAsync(
            productIds,
            plan.ProductionDate.Date.AddDays(1));

        foreach (var line in plan.Lines.OrderBy(l => l.ProductId))
        {
            if (line.PlannedQuantity <= 0)
            {
                continue;
            }

            if (await _orderRepository.FindByPlanLineAsync(line.Id) != null)
            {
                throw new BusinessException(ProductionErrorCodes.ProductionOrderAlreadyExistsForPlanLine)
                    .WithData("ProductionPlanLineId", line.Id);
            }

            var order = await CreateAsync(
                plan.KitchenBranchId,
                plan.Id,
                line.Id,
                line.ProductId,
                line.PlannedQuantity,
                ProductionPriorities.Normal,
                createdByUserId,
                $"From plan {plan.PlanNumber}",
                sequence++);

            var remainingForRequests = line.PlannedQuantity;
            foreach (var target in planningTargets.Where(x =>
                         x.ProductId == line.ProductId && x.RemainingUnplannedQuantity > 0))
            {
                if (remainingForRequests <= 0)
                {
                    break;
                }

                if (!requestsById.TryGetValue(target.RequestId, out var request))
                {
                    request = await _requestRepository.GetWithItemsAsync(target.RequestId);
                    requestsById[target.RequestId] = request;
                }

                var allocated = Math.Min(remainingForRequests, target.RemainingUnplannedQuantity);
                request.ReservePlannedQuantity(target.RequestItemId, allocated);
                order.AddAllocation(
                    GuidGenerator.Create(),
                    target.BranchId,
                    target.RequestId,
                    target.RequestItemId,
                    allocated);

                target.RemainingUnplannedQuantity -= allocated;
                remainingForRequests -= allocated;
            }

            result.Add(order);
        }

        if (result.Count == 0)
        {
            throw new BusinessException(ProductionErrorCodes.NoPlannedQuantityForProductionOrder);
        }

        foreach (var request in requestsById.Values)
        {
            await _requestRepository.UpdateAsync(request);
        }

        return result;
    }

    public async Task ApplyAllocationReleasesAsync(
        IEnumerable<AppProductionOrder.AllocationReleaseLine> releaseLines)
    {
        var requests = new Dictionary<Guid, AppBranchProductionRequest>();
        foreach (var line in releaseLines)
        {
            if (!requests.TryGetValue(line.BranchProductionRequestId, out var request))
            {
                request = await _requestRepository.GetWithItemsAsync(line.BranchProductionRequestId);
                requests[line.BranchProductionRequestId] = request;
            }

            request.ReleasePlannedQuantity(line.BranchProductionRequestItemId, line.Quantity);
        }

        foreach (var request in requests.Values)
        {
            await _requestRepository.UpdateAsync(request);
        }
    }

    public async Task ApplyDispatchResultsAsync(
        IEnumerable<AppProductionOrder.DispatchResultLine> resultLines)
    {
        var requests = new Dictionary<Guid, AppBranchProductionRequest>();
        foreach (var line in resultLines)
        {
            if (!line.BranchProductionRequestId.HasValue
                || !line.BranchProductionRequestItemId.HasValue)
            {
                continue;
            }

            if (!requests.TryGetValue(line.BranchProductionRequestId.Value, out var request))
            {
                request = await _requestRepository.GetWithItemsAsync(line.BranchProductionRequestId.Value);
                requests[line.BranchProductionRequestId.Value] = request;
            }

            if (line.ReceivedQuantity > 0)
            {
                request.AddFulfilledQuantity(
                    line.BranchProductionRequestItemId.Value,
                    line.ReceivedQuantity);
            }
            if (line.LostQuantity > 0)
            {
                request.ReleasePlannedQuantity(
                    line.BranchProductionRequestItemId.Value,
                    line.LostQuantity);
            }
        }

        foreach (var request in requests.Values)
        {
            await _requestRepository.UpdateAsync(request);
        }
    }

    public async Task<AppProductionOrder> CreateAsync(
        Guid kitchenBranchId,
        Guid? productionPlanId,
        Guid? productionPlanLineId,
        Guid finishedProductId,
        int plannedOutputQuantity,
        string priority,
        Guid? createdByUserId,
        string? notes,
        int sequence = 1)
    {
        if (plannedOutputQuantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("PlannedOutputQuantity", plannedOutputQuantity);
        }

        var formula = await _formulaRepository.GetActiveDefaultForProductAsync(finishedProductId)
            ?? throw new BusinessException(ProductionErrorCodes.FormulaRequiredForProductionOrder)
                .WithData("FinishedProductId", finishedProductId);

        formula = await _formulaRepository.GetWithItemsAsync(formula.Id);

        var ingredientIds = formula.Items.Select(i => i.IngredientProductId).Distinct().ToList();
        var unitCosts = await _formulaRepository.GetIngredientCostPricesAsync(ingredientIds);
        var cost = ProductionCostCalculator.Calculate(formula, plannedOutputQuantity, unitCosts);

        var order = new AppProductionOrder(
            GuidGenerator.Create(),
            CreateOrderNumber(sequence),
            kitchenBranchId,
            productionPlanId,
            productionPlanLineId,
            finishedProductId,
            formula.Id,
            formula.Version,
            priority,
            plannedOutputQuantity,
            cost.PlannedIngredientCost,
            cost.LaborCost,
            cost.OverheadCost,
            createdByUserId,
            notes);

        foreach (var line in cost.Lines)
        {
            order.AddIngredientSnapshot(
                GuidGenerator.Create(),
                line.IngredientProductId,
                line.RequiredQuantity,
                line.UnitCost);
        }

        var availability = await CheckAvailabilityAsync(order);
        order.SetIngredientAvailability(availability.Any(a => a.HasShortage));

        return order;
    }

    public async Task<List<ProductionIngredientAvailability>> CheckAvailabilityAsync(AppProductionOrder order)
    {
        Check.NotNull(order, nameof(order));

        var ingredientIds = order.Ingredients.Select(i => i.IngredientProductId).Distinct().ToList();
        if (ingredientIds.Count == 0)
        {
            return new List<ProductionIngredientAvailability>();
        }

        var inventories = await _inventoryRepository.GetListAsync(
            i => i.BranchId == order.KitchenBranchId && ingredientIds.Contains(i.ProductId));
        var inventoryByProduct = inventories.ToDictionary(i => i.ProductId);

        var products = await _productRepository.GetListAsync(p => ingredientIds.Contains(p.Id));
        var productById = products.ToDictionary(p => p.Id);
        var nonExpired = await _stockBatchRepository.GetNonExpiredQuantitiesByProductAsync(
            order.KitchenBranchId, Clock.Now.ToUniversalTime().Date);

        var result = new List<ProductionIngredientAvailability>();
        foreach (var ingredient in order.Ingredients.OrderBy(i => i.IngredientProductId))
        {
            inventoryByProduct.TryGetValue(ingredient.IngredientProductId, out var inventory);
            productById.TryGetValue(ingredient.IngredientProductId, out var product);

            result.Add(new ProductionIngredientAvailability
            {
                IngredientProductId = ingredient.IngredientProductId,
                IngredientName = product?.Name ?? ingredient.IngredientProductId.ToString(),
                IngredientSku = product?.SKU ?? string.Empty,
                Unit = product?.Unit ?? string.Empty,
                RequiredQuantity = ingredient.RequiredQuantity,
                AvailableQuantity = inventory == null || product == null
                    ? 0
                    : StockBatchManager.GetUsableQuantity(product, inventory, nonExpired)
            });
        }

        return result;
    }

    public async Task RefreshAvailabilityAsync(AppProductionOrder order)
    {
        var availability = await CheckAvailabilityAsync(order);
        order.SetIngredientAvailability(availability.Any(a => a.HasShortage));
    }

    private static string CreateOrderNumber(int sequence) =>
        $"PROD-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{sequence:D3}";
}
