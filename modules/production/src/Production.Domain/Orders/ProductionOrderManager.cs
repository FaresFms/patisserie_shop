using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
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

    public ProductionOrderManager(
        IProductionOrderRepository orderRepository,
        IProductionFormulaRepository formulaRepository,
        IRepository<AppBranchInventory, Guid> inventoryRepository,
        IRepository<AppProduct, Guid> productRepository)
    {
        _orderRepository = orderRepository;
        _formulaRepository = formulaRepository;
        _inventoryRepository = inventoryRepository;
        _productRepository = productRepository;
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
        var sequence = 1;

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

            result.Add(order);
        }

        if (result.Count == 0)
        {
            throw new BusinessException(ProductionErrorCodes.NoPlannedQuantityForProductionOrder);
        }

        return result;
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
                AvailableQuantity = inventory?.QuantityOnHand ?? 0
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
