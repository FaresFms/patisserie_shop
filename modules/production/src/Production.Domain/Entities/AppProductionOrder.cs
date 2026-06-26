using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Production.Entities;

public class AppProductionOrder : FullAuditedAggregateRoot<Guid>
{
    public string OrderNumber { get; private set; } = null!;
    public Guid KitchenBranchId { get; private set; }
    public Guid? ProductionPlanId { get; private set; }
    public Guid? ProductionPlanLineId { get; private set; }
    public Guid FinishedProductId { get; private set; }
    public Guid FormulaId { get; private set; }
    public int FormulaVersion { get; private set; }
    public string Status { get; private set; } = ProductionOrderStatuses.Draft;
    public string Priority { get; private set; } = ProductionPriorities.Normal;
    public int PlannedOutputQuantity { get; private set; }
    public int ActualOutputQuantity { get; private set; }
    public int AcceptedQuantity { get; private set; }
    public int RejectedQuantity { get; private set; }
    public DateTime? PlannedStartTime { get; private set; }
    public DateTime? ActualStartTime { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? ExpiryDate { get; private set; }
    public decimal PlannedIngredientCost { get; private set; }
    public decimal ActualIngredientCost { get; private set; }
    public decimal LaborCost { get; private set; }
    public decimal OverheadCost { get; private set; }
    public decimal TotalProductionCost { get; private set; }
    public decimal UnitProductionCost { get; private set; }
    public string? WasteReason { get; private set; }
    public string? Notes { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? StartedByUserId { get; private set; }
    public Guid? CompletedByUserId { get; private set; }

    private readonly List<AppProductionOrderIngredient> _ingredients = new();
    public IReadOnlyCollection<AppProductionOrderIngredient> Ingredients => new ReadOnlyCollection<AppProductionOrderIngredient>(_ingredients);

    private readonly List<AppProductionOrderAllocation> _allocations = new();
    public IReadOnlyCollection<AppProductionOrderAllocation> Allocations => new ReadOnlyCollection<AppProductionOrderAllocation>(_allocations);

    protected AppProductionOrder() { }

    internal AppProductionOrder(
        Guid id,
        string orderNumber,
        Guid kitchenBranchId,
        Guid? productionPlanId,
        Guid? productionPlanLineId,
        Guid finishedProductId,
        Guid formulaId,
        int formulaVersion,
        string priority,
        int plannedOutputQuantity,
        decimal plannedIngredientCost,
        decimal laborCost,
        decimal overheadCost,
        Guid? createdByUserId,
        string? notes = null)
        : base(id)
    {
        OrderNumber = Check.NotNullOrWhiteSpace(orderNumber, nameof(orderNumber), maxLength: 32);
        KitchenBranchId = kitchenBranchId;
        ProductionPlanId = productionPlanId;
        ProductionPlanLineId = productionPlanLineId;
        FinishedProductId = finishedProductId;
        FormulaId = formulaId;
        FormulaVersion = formulaVersion;
        Priority = ProductionPriorities.IsValid(priority) ? priority : ProductionPriorities.Normal;
        SetPlannedOutputQuantity(plannedOutputQuantity);
        PlannedIngredientCost = Math.Max(0m, plannedIngredientCost);
        LaborCost = Math.Max(0m, laborCost);
        OverheadCost = Math.Max(0m, overheadCost);
        TotalProductionCost = PlannedIngredientCost + LaborCost + OverheadCost;
        UnitProductionCost = TotalProductionCost / PlannedOutputQuantity;
        CreatedByUserId = createdByUserId;
        Notes = notes;
        Status = ProductionOrderStatuses.Draft;
    }

    public AppProductionOrderIngredient AddIngredientSnapshot(
        Guid ingredientId,
        Guid ingredientProductId,
        int requiredQuantity,
        decimal unitCostSnapshot)
    {
        if (Status != ProductionOrderStatuses.Draft)
        {
            throw InvalidTransition(ProductionOrderStatuses.Draft);
        }

        if (_ingredients.Any(i => i.IngredientProductId == ingredientProductId))
        {
            throw new BusinessException(ProductionErrorCodes.DuplicateIngredient)
                .WithData("IngredientProductId", ingredientProductId);
        }

        var ingredient = new AppProductionOrderIngredient(
            ingredientId,
            Id,
            ingredientProductId,
            requiredQuantity,
            unitCostSnapshot);
        _ingredients.Add(ingredient);
        return ingredient;
    }

    public AppProductionOrderAllocation AddAllocation(
        Guid allocationId,
        Guid branchId,
        Guid? branchProductionRequestItemId,
        int allocatedQuantity)
    {
        if (Status != ProductionOrderStatuses.Draft)
        {
            throw InvalidTransition(ProductionOrderStatuses.Draft);
        }

        var allocation = new AppProductionOrderAllocation(
            allocationId,
            Id,
            branchId,
            branchProductionRequestItemId,
            allocatedQuantity);
        _allocations.Add(allocation);
        return allocation;
    }

    public void SetIngredientAvailability(bool hasShortage)
    {
        if (Status != ProductionOrderStatuses.Draft
            && Status != ProductionOrderStatuses.ReadyToCook
            && Status != ProductionOrderStatuses.WaitingForIngredients)
        {
            throw InvalidTransition(hasShortage
                ? ProductionOrderStatuses.WaitingForIngredients
                : ProductionOrderStatuses.ReadyToCook);
        }

        Status = hasShortage
            ? ProductionOrderStatuses.WaitingForIngredients
            : ProductionOrderStatuses.ReadyToCook;
    }

    public IReadOnlyList<IngredientConsumptionLine> Start(Guid? startedByUserId)
    {
        if (Status != ProductionOrderStatuses.ReadyToCook)
        {
            throw InvalidTransition(ProductionOrderStatuses.InProduction);
        }
        if (_ingredients.Count == 0)
        {
            throw new BusinessException(ProductionErrorCodes.FormulaRequiredForProductionOrder);
        }

        var lines = new List<IngredientConsumptionLine>(_ingredients.Count);
        foreach (var ingredient in _ingredients)
        {
            ingredient.MarkConsumed();
            lines.Add(new IngredientConsumptionLine(
                ingredient.Id,
                ingredient.IngredientProductId,
                ingredient.ConsumedQuantity,
                ingredient.UnitCostSnapshot,
                ingredient.TotalCost));
        }

        ActualIngredientCost = _ingredients.Sum(i => i.TotalCost);
        TotalProductionCost = ActualIngredientCost + LaborCost + OverheadCost;
        UnitProductionCost = TotalProductionCost / PlannedOutputQuantity;
        StartedByUserId = startedByUserId;
        ActualStartTime = DateTime.UtcNow;
        Status = ProductionOrderStatuses.InProduction;

        return lines;
    }

    public OutputLine Complete(
        int actualOutputQuantity,
        int acceptedQuantity,
        int rejectedQuantity,
        DateTime expiryDate,
        string? wasteReason,
        Guid? completedByUserId,
        string? notes)
    {
        if (Status != ProductionOrderStatuses.InProduction)
        {
            throw InvalidTransition(ProductionOrderStatuses.Completed);
        }
        if (actualOutputQuantity <= 0 || acceptedQuantity < 0 || rejectedQuantity < 0
            || acceptedQuantity + rejectedQuantity != actualOutputQuantity)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("ActualOutputQuantity", actualOutputQuantity)
                .WithData("AcceptedQuantity", acceptedQuantity)
                .WithData("RejectedQuantity", rejectedQuantity);
        }
        if (acceptedQuantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.CannotCompleteWithoutAcceptedQuantity);
        }
        if (rejectedQuantity > 0 && string.IsNullOrWhiteSpace(wasteReason))
        {
            throw new BusinessException(ProductionErrorCodes.WasteReasonRequired);
        }

        ActualOutputQuantity = actualOutputQuantity;
        AcceptedQuantity = acceptedQuantity;
        RejectedQuantity = rejectedQuantity;
        ExpiryDate = expiryDate.Date;
        WasteReason = rejectedQuantity > 0 ? wasteReason?.Trim() : null;
        CompletedByUserId = completedByUserId;
        CompletedAt = DateTime.UtcNow;
        Notes = notes;
        UnitProductionCost = TotalProductionCost / AcceptedQuantity;
        Status = ProductionOrderStatuses.Completed;

        return new OutputLine(FinishedProductId, AcceptedQuantity, UnitProductionCost, ExpiryDate.Value);
    }

    public void Cancel()
    {
        if (!ProductionOrderStatuses.CanCancel(Status))
        {
            throw InvalidTransition(ProductionOrderStatuses.Cancelled);
        }

        Status = ProductionOrderStatuses.Cancelled;
    }

    private void SetPlannedOutputQuantity(int plannedOutputQuantity)
    {
        if (plannedOutputQuantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("PlannedOutputQuantity", plannedOutputQuantity);
        }

        PlannedOutputQuantity = plannedOutputQuantity;
    }

    private BusinessException InvalidTransition(string targetStatus) =>
        new BusinessException(ProductionErrorCodes.InvalidOrderStatusTransition)
            .WithData("CurrentStatus", Status)
            .WithData("TargetStatus", targetStatus);

    public record IngredientConsumptionLine(
        Guid IngredientId,
        Guid IngredientProductId,
        int Quantity,
        decimal UnitCost,
        decimal TotalCost);

    public record OutputLine(Guid ProductId, int AcceptedQuantity, decimal UnitCost, DateTime ExpiryDate);
}
