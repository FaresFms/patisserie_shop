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

    private readonly List<AppProductionOrderDispatch> _dispatches = new();
    public IReadOnlyCollection<AppProductionOrderDispatch> Dispatches => new ReadOnlyCollection<AppProductionOrderDispatch>(_dispatches);

    public int ReservedQuantity => _allocations.Sum(x => x.AllocatedQuantity);
    public int DispatchedQuantity => _allocations.Sum(x => x.DispatchedQuantity);
    public int ReceivedQuantity => _allocations.Sum(x => x.ReceivedQuantity);
    public int LostQuantity => _allocations.Sum(x => x.LostQuantity);
    public int InTransitQuantity => _allocations.Sum(x => x.InTransitQuantity);
    public int RemainingToDispatch => Math.Max(0, ReservedQuantity - DispatchedQuantity);

    public int GetRemainingToDispatch(Guid destinationBranchId) => _allocations
        .Where(x => x.BranchId == destinationBranchId)
        .Sum(x => x.RemainingToDispatch);

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
        Guid? branchProductionRequestId,
        Guid? branchProductionRequestItemId,
        int allocatedQuantity)
    {
        if (Status != ProductionOrderStatuses.Draft
            && Status != ProductionOrderStatuses.ReadyToCook
            && Status != ProductionOrderStatuses.WaitingForIngredients)
        {
            throw InvalidTransition(ProductionOrderStatuses.Draft);
        }

        var allocation = new AppProductionOrderAllocation(
            allocationId,
            Id,
            branchId,
            branchProductionRequestId,
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

    public CompletionResult Complete(
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
        if (expiryDate.Date < (ActualStartTime?.Date ?? DateTime.UtcNow.Date))
        {
            throw new BusinessException(ProductionErrorCodes.ProductionExpiryDateInPast)
                .WithData("ExpiryDate", expiryDate.Date)
                .WithData("ProductionDate", ActualStartTime?.Date ?? DateTime.UtcNow.Date);
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

        var releases = ReleaseAllocationExcess(AcceptedQuantity);
        return new CompletionResult(
            new OutputLine(FinishedProductId, AcceptedQuantity, UnitProductionCost, ExpiryDate.Value),
            releases);
    }

    public IReadOnlyList<AllocationReleaseLine> Cancel()
    {
        if (!ProductionOrderStatuses.CanCancel(Status))
        {
            throw InvalidTransition(ProductionOrderStatuses.Cancelled);
        }

        var releases = ReleaseAllocationExcess(0);
        Status = ProductionOrderStatuses.Cancelled;
        return releases;
    }

    public AppProductionOrderDispatch CreateDispatch(
        Guid dispatchId,
        Guid stockTransferId,
        Guid destinationBranchId,
        int quantity,
        DateTime dispatchedAt,
        Func<Guid> lineIdFactory)
    {
        if (Status != ProductionOrderStatuses.Completed)
        {
            throw InvalidTransition(ProductionOrderStatuses.Completed);
        }
        if (quantity <= 0 || quantity > RemainingToDispatch)
        {
            throw new BusinessException(ProductionErrorCodes.DispatchQuantityExceedsRemaining)
                .WithData("RequestedQuantity", quantity)
                .WithData("RemainingToDispatch", RemainingToDispatch);
        }
        if (_dispatches.Any(x => x.StockTransferId == stockTransferId))
        {
            throw new BusinessException(ProductionErrorCodes.ProductionDispatchAlreadyExists)
                .WithData("StockTransferId", stockTransferId);
        }

        var candidates = _allocations
            .Where(x => x.BranchId == destinationBranchId && x.RemainingToDispatch > 0)
            .ToList();
        var destinationRemaining = candidates.Sum(x => x.RemainingToDispatch);
        if (quantity > destinationRemaining)
        {
            throw new BusinessException(ProductionErrorCodes.DispatchDestinationHasNoAllocation)
                .WithData("DestinationBranchId", destinationBranchId)
                .WithData("RequestedQuantity", quantity)
                .WithData("DestinationRemaining", destinationRemaining);
        }

        var dispatch = new AppProductionOrderDispatch(
            dispatchId,
            Id,
            stockTransferId,
            destinationBranchId,
            quantity,
            dispatchedAt);

        var remaining = quantity;
        foreach (var allocation in candidates)
        {
            if (remaining <= 0)
            {
                break;
            }

            var allocated = Math.Min(remaining, allocation.RemainingToDispatch);
            allocation.MarkDispatched(allocated);
            dispatch.AddLine(
                lineIdFactory(),
                allocation.Id,
                allocation.BranchProductionRequestId,
                allocation.BranchProductionRequestItemId,
                allocated);
            remaining -= allocated;
        }

        _dispatches.Add(dispatch);
        return dispatch;
    }

    public IReadOnlyList<DispatchResultLine> CompleteDispatch(
        Guid stockTransferId,
        int receivedQuantity,
        DateTime completedAt)
    {
        var dispatch = _dispatches.FirstOrDefault(x => x.StockTransferId == stockTransferId)
            ?? throw new BusinessException(ProductionErrorCodes.ProductionDispatchNotFound)
                .WithData("StockTransferId", stockTransferId);

        var receiptLines = dispatch.Complete(receivedQuantity, completedAt);
        if (receiptLines.Count == 0)
        {
            return Array.Empty<DispatchResultLine>();
        }

        var results = new List<DispatchResultLine>(receiptLines.Count);
        foreach (var receipt in receiptLines)
        {
            var allocation = _allocations.FirstOrDefault(x => x.Id == receipt.ProductionOrderAllocationId)
                ?? throw new BusinessException(ProductionErrorCodes.ProductionDispatchAllocationNotFound)
                    .WithData("ProductionOrderAllocationId", receipt.ProductionOrderAllocationId);

            allocation.RecordTransferResult(receipt.ReceivedQuantity, receipt.LostQuantity);
            results.Add(new DispatchResultLine(
                allocation.BranchProductionRequestId,
                receipt.BranchProductionRequestItemId,
                receipt.ReceivedQuantity,
                receipt.LostQuantity));
        }

        return results;
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

    private IReadOnlyList<AllocationReleaseLine> ReleaseAllocationExcess(int quantityToKeep)
    {
        var excess = Math.Max(0, ReservedQuantity - quantityToKeep);
        if (excess == 0)
        {
            return Array.Empty<AllocationReleaseLine>();
        }

        var releases = new List<AllocationReleaseLine>();
        for (var index = _allocations.Count - 1; index >= 0 && excess > 0; index--)
        {
            var allocation = _allocations[index];
            var release = Math.Min(excess, allocation.RemainingToDispatch);
            if (release <= 0)
            {
                continue;
            }

            allocation.ReleaseUnshippedQuantity(release);
            excess -= release;
            if (allocation.BranchProductionRequestId.HasValue
                && allocation.BranchProductionRequestItemId.HasValue)
            {
                releases.Add(new AllocationReleaseLine(
                    allocation.BranchProductionRequestId.Value,
                    allocation.BranchProductionRequestItemId.Value,
                    release));
            }
        }

        if (excess > 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("UnreleasedQuantity", excess);
        }

        return releases;
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
    public record CompletionResult(OutputLine Output, IReadOnlyList<AllocationReleaseLine> ReleasedAllocations);
    public record AllocationReleaseLine(
        Guid BranchProductionRequestId,
        Guid BranchProductionRequestItemId,
        int Quantity);
    public record DispatchResultLine(
        Guid? BranchProductionRequestId,
        Guid? BranchProductionRequestItemId,
        int ReceivedQuantity,
        int LostQuantity);
}
