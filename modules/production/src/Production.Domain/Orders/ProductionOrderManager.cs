using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.Entities;
using Inventory.StockBatches;
using Microsoft.Extensions.Localization;
using Production.BranchRequests;
using Production.Costing;
using Production.Entities;
using Production.Formulas;
using Production.Localization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Localization;

namespace Production.Orders;

public class ProductionOrderManager : DomainService
{
    private readonly IProductionOrderRepository _orderRepository;
    private readonly IProductionFormulaRepository _formulaRepository;
    private readonly IRepository<AppBranchInventory, Guid> _inventoryRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IStockBatchRepository _stockBatchRepository;
    private readonly IBranchProductionRequestRepository _requestRepository;
    private readonly IStringLocalizer<ProductionResource> _localizer;

    public ProductionOrderManager(
        IProductionOrderRepository orderRepository,
        IProductionFormulaRepository formulaRepository,
        IRepository<AppBranchInventory, Guid> inventoryRepository,
        IRepository<AppProduct, Guid> productRepository,
        IStockBatchRepository stockBatchRepository,
        IBranchProductionRequestRepository requestRepository,
        IStringLocalizer<ProductionResource> localizer)
    {
        _orderRepository = orderRepository;
        _formulaRepository = formulaRepository;
        _inventoryRepository = inventoryRepository;
        _productRepository = productRepository;
        _stockBatchRepository = stockBatchRepository;
        _requestRepository = requestRepository;
        _localizer = localizer;
    }

    public void EnsureScheduleDuration(
        AppProductionOrder order,
        int capacityUnitsPerHour,
        DateTime scheduledStart,
        DateTime scheduledEnd)
    {
        Check.NotNull(order, nameof(order));
        if (capacityUnitsPerHour <= 0 || scheduledEnd <= scheduledStart)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidProductionSchedule);
        }

        var availableUnits = (scheduledEnd - scheduledStart).TotalHours * capacityUnitsPerHour;
        if (availableUnits + 0.0001d < order.PlannedOutputQuantity)
        {
            throw new BusinessException(ProductionErrorCodes.ProductionCapacityExceeded)
                .WithData("PlannedQuantity", order.PlannedOutputQuantity)
                .WithData("CapacityUnitsPerHour", capacityUnitsPerHour)
                .WithData("RequiredMinutes", Math.Ceiling(order.PlannedOutputQuantity / (double)capacityUnitsPerHour * 60d));
        }
    }

    public async Task<List<AppProductionOrder>> CreateFromPlanAsync(
        AppProductionPlan plan,
        Guid? createdByUserId)
    {
        using var contentCulture = CultureHelper.Use("ar-SY", "ar-SY");
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
                _localizer["ProductionOrderFromPlanNote", plan.PlanNumber],
                sequence++);

            // Existing uncommitted kitchen stock covers approved branch demand first.
            // Only the part of request demand not covered by that stock is allocated
            // to this new cook order; any remaining production is forecast/buffer stock.
            var requestProductionQuantity = Math.Max(
                0,
                line.RequestedQuantity - line.CurrentKitchenStock);
            var remainingForRequests = Math.Min(
                line.PlannedQuantity,
                requestProductionQuantity);
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
        formula.EnsureHasIngredients();

        var ingredientIds = formula.Items.Select(i => i.IngredientProductId).Distinct().ToList();
        var unitCosts = await _formulaRepository.GetIngredientCostPricesAsync(ingredientIds);
        var cost = ProductionCostCalculator.Calculate(formula, plannedOutputQuantity, unitCosts);
        EnsureIngredientCosts(cost);

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

        var formulaAllergens = string.Join(", ", formula.GetIngredientAllergens().Values
            .SelectMany(value => value.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase));
        order.SetRecipeControlSnapshot(formula.WorkCenterCode, formulaAllergens);

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

    public async Task RefreshIngredientCostSnapshotsAsync(AppProductionOrder order)
    {
        Check.NotNull(order, nameof(order));
        var ingredientIds = order.Ingredients.Select(i => i.IngredientProductId).Distinct().ToList();
        var unitCosts = await _formulaRepository.GetIngredientCostPricesAsync(ingredientIds);
        order.RefreshIngredientCostSnapshots(unitCosts);
    }

    public void RecordActualIngredientCost(
        AppProductionOrder order,
        AppProductionOrder.IngredientConsumptionLine consumption,
        IReadOnlyList<ConsumedStockBatchLine> consumedBatches)
    {
        Check.NotNull(order, nameof(order));
        var coveredQuantity = 0;
        var actualCost = 0m;
        foreach (var batch in consumedBatches)
        {
            coveredQuantity += batch.Quantity;
            actualCost += batch.Quantity * (batch.UnitCost > 0m ? batch.UnitCost : consumption.UnitCost);
        }

        if (coveredQuantity < consumption.Quantity)
        {
            actualCost += (consumption.Quantity - coveredQuantity) * consumption.UnitCost;
        }

        order.RecordActualIngredientCost(consumption.IngredientId, actualCost);
        order.RecordIngredientLotConsumption(
            consumption.IngredientProductId,
            consumedBatches.Select(batch => new AppProductionOrder.IngredientLotLine(
                consumption.IngredientProductId,
                batch.BatchId,
                batch.BatchNumber,
                batch.ExpiryDate,
                batch.Quantity,
                batch.UnitCost)).ToList());
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

    public async Task<List<AppProductionOrder>> CreateSubProductionOrdersAsync(
        AppProductionOrder parent,
        Guid? createdByUserId)
    {
        Check.NotNull(parent, nameof(parent));
        var existing = await _orderRepository.GetChildrenAsync(parent.Id, parent.KitchenBranchId);
        if (existing.Count > 0)
        {
            throw new BusinessException(ProductionErrorCodes.SubProductionAlreadyExists)
                .WithData("ProductionOrderId", parent.Id);
        }

        var shortages = (await CheckAvailabilityAsync(parent))
            .Where(line => line.HasShortage)
            .ToList();
        if (shortages.Count == 0)
        {
            throw new BusinessException(ProductionErrorCodes.NoSemiFinishedShortage);
        }

        var shortageIds = shortages.Select(line => line.IngredientProductId).Distinct().ToList();
        var semiFinishedProducts = await _productRepository.GetListAsync(product =>
            shortageIds.Contains(product.Id)
            && product.ProductType == ProductTypes.SemiFinished
            && product.IsProducible
            && product.IsActive);
        var semiFinishedIds = semiFinishedProducts.Select(product => product.Id).ToHashSet();

        var result = new List<AppProductionOrder>();
        var sequence = 1;
        foreach (var shortage in shortages.Where(line => semiFinishedIds.Contains(line.IngredientProductId)))
        {
            var child = await CreateAsync(
                parent.KitchenBranchId,
                productionPlanId: null,
                productionPlanLineId: null,
                shortage.IngredientProductId,
                shortage.ShortageQuantity,
                parent.Priority,
                createdByUserId,
                $"Sub-production for {parent.OrderNumber}",
                sequence++);
            child.LinkParentProductionOrder(parent.Id);
            result.Add(child);
        }

        if (result.Count == 0)
        {
            throw new BusinessException(ProductionErrorCodes.NoSemiFinishedShortage);
        }
        return result;
    }

    private static string CreateOrderNumber(int sequence) =>
        $"PROD-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{sequence:D3}";

    private static void EnsureIngredientCosts(ProductionCostResult cost)
    {
        var missingCost = cost.Lines.FirstOrDefault(line => line.HasZeroCost);
        if (missingCost != null)
        {
            throw new BusinessException(ProductionErrorCodes.IngredientCostRequired)
                .WithData("IngredientProductId", missingCost.IngredientProductId);
        }
    }
}
