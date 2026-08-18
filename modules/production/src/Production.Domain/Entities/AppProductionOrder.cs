using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities.Auditing;

namespace Production.Entities;

public class AppProductionOrder : FullAuditedAggregateRoot<Guid>
{
    private const string ParentOrderProperty = "Production.Order.ParentOrderId";
    private const string WorkCenterProperty = "Production.Order.WorkCenterCode";
    private const string ShiftProperty = "Production.Order.ShiftCode";
    private const string OperatorIdProperty = "Production.Order.OperatorUserId";
    private const string OperatorNameProperty = "Production.Order.OperatorName";
    private const string ScheduledStartProperty = "Production.Order.ScheduledStart";
    private const string ScheduledEndProperty = "Production.Order.ScheduledEnd";
    private const string QualityStatusProperty = "Production.Order.QualityStatus";
    private const string QualityReasonProperty = "Production.Order.QualityReason";
    private const string QualityUpdatedAtProperty = "Production.Order.QualityUpdatedAt";
    private const string QualityUpdatedByProperty = "Production.Order.QualityUpdatedBy";
    private const string IngredientLotsProperty = "Production.Order.IngredientLots";
    private const string OutputBatchIdProperty = "Production.Order.OutputBatchId";
    private const string OutputBatchNumberProperty = "Production.Order.OutputBatchNumber";
    private const string FormulaAllergensProperty = "Production.Order.FormulaAllergens";

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

    public Guid? ParentProductionOrderId => this.GetProperty<Guid?>(ParentOrderProperty);
    public string? WorkCenterCode => this.GetProperty<string>(WorkCenterProperty);
    public string? ShiftCode => this.GetProperty<string>(ShiftProperty);
    public Guid? AssignedOperatorUserId => this.GetProperty<Guid?>(OperatorIdProperty);
    public string? AssignedOperatorName => this.GetProperty<string>(OperatorNameProperty);
    public DateTime? ScheduledStartTime => this.GetProperty<DateTime?>(ScheduledStartProperty);
    public DateTime? ScheduledEndTime => this.GetProperty<DateTime?>(ScheduledEndProperty);
    public string FormulaAllergens => this.GetProperty<string>(FormulaAllergensProperty) ?? string.Empty;
    public string QualityStatus => this.GetProperty<string>(QualityStatusProperty)
        ?? (Status == ProductionOrderStatuses.Completed
            ? ProductionQualityStatuses.Released
            : ProductionQualityStatuses.NotRequired);
    public string? QualityReason => this.GetProperty<string>(QualityReasonProperty);
    public DateTime? QualityUpdatedAt => this.GetProperty<DateTime?>(QualityUpdatedAtProperty);
    public Guid? QualityUpdatedByUserId => this.GetProperty<Guid?>(QualityUpdatedByProperty);
    public Guid? OutputBatchId => this.GetProperty<Guid?>(OutputBatchIdProperty);
    public string? OutputBatchNumber => this.GetProperty<string>(OutputBatchNumberProperty);

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

    public void RefreshIngredientCostSnapshots(IReadOnlyDictionary<Guid, decimal> unitCosts)
    {
        if (Status != ProductionOrderStatuses.Draft
            && Status != ProductionOrderStatuses.ReadyToCook
            && Status != ProductionOrderStatuses.WaitingForIngredients)
        {
            throw InvalidTransition(ProductionOrderStatuses.ReadyToCook);
        }

        foreach (var ingredient in _ingredients)
        {
            if (!unitCosts.TryGetValue(ingredient.IngredientProductId, out var unitCost)
                || unitCost <= 0m)
            {
                throw new BusinessException(ProductionErrorCodes.IngredientCostRequired)
                    .WithData("IngredientProductId", ingredient.IngredientProductId);
            }

            ingredient.RefreshCostSnapshot(unitCost);
        }
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

    public void SetRecipeControlSnapshot(string? workCenterCode, string? formulaAllergens)
    {
        EnsurePreStartState();
        ExtraProperties[WorkCenterProperty] = NormalizeOptional(workCenterCode);
        ExtraProperties[FormulaAllergensProperty] = NormalizeOptional(formulaAllergens) ?? string.Empty;
    }

    public void LinkParentProductionOrder(Guid parentProductionOrderId)
    {
        EnsurePreStartState();
        ExtraProperties[ParentOrderProperty] = parentProductionOrderId;
    }

    public void Schedule(
        string workCenterCode,
        string shiftCode,
        DateTime scheduledStart,
        DateTime scheduledEnd,
        Guid operatorUserId,
        string operatorName)
    {
        EnsurePreStartState();
        if (string.IsNullOrWhiteSpace(workCenterCode)
            || string.IsNullOrWhiteSpace(shiftCode)
            || scheduledEnd <= scheduledStart)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidProductionSchedule);
        }
        if (operatorUserId == Guid.Empty || string.IsNullOrWhiteSpace(operatorName))
        {
            throw new BusinessException(ProductionErrorCodes.ProductionOperatorRequired);
        }

        ExtraProperties[WorkCenterProperty] = workCenterCode.Trim();
        ExtraProperties[ShiftProperty] = shiftCode.Trim();
        ExtraProperties[ScheduledStartProperty] = scheduledStart;
        ExtraProperties[ScheduledEndProperty] = scheduledEnd;
        ExtraProperties[OperatorIdProperty] = operatorUserId;
        ExtraProperties[OperatorNameProperty] = operatorName.Trim();
        PlannedStartTime = scheduledStart;
    }

    public void EnsureReadyForStart(bool requireSchedule, bool requireOperator)
    {
        if (requireSchedule && (!ScheduledStartTime.HasValue || !ScheduledEndTime.HasValue))
        {
            throw new BusinessException(ProductionErrorCodes.ProductionScheduleRequired);
        }
        if (requireOperator && !AssignedOperatorUserId.HasValue)
        {
            throw new BusinessException(ProductionErrorCodes.ProductionOperatorRequired);
        }
    }

    public void RecordIngredientLotConsumption(
        Guid ingredientProductId,
        IReadOnlyCollection<IngredientLotLine> lots)
    {
        if (Status != ProductionOrderStatuses.InProduction)
        {
            throw InvalidTransition(ProductionOrderStatuses.InProduction);
        }

        var all = GetIngredientLots().ToList();
        all.RemoveAll(x => x.IngredientProductId == ingredientProductId);
        all.AddRange(lots.Select(x => x with { IngredientProductId = ingredientProductId }));
        ExtraProperties[IngredientLotsProperty] = JsonSerializer.Serialize(all);
    }

    public IReadOnlyList<IngredientLotLine> GetIngredientLots()
    {
        var json = this.GetProperty<string>(IngredientLotsProperty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<IngredientLotLine>();
        }
        try
        {
            return JsonSerializer.Deserialize<List<IngredientLotLine>>(json)
                ?? new List<IngredientLotLine>();
        }
        catch (JsonException)
        {
            return Array.Empty<IngredientLotLine>();
        }
    }

    public void RecordOutputBatch(Guid batchId, string batchNumber)
    {
        if (Status != ProductionOrderStatuses.Completed)
        {
            throw InvalidTransition(ProductionOrderStatuses.Completed);
        }
        ExtraProperties[OutputBatchIdProperty] = batchId;
        ExtraProperties[OutputBatchNumberProperty] = Check.NotNullOrWhiteSpace(
            batchNumber,
            nameof(batchNumber),
            maxLength: 32);
    }

    public void RecordActualIngredientCost(Guid ingredientId, decimal actualTotalCost)
    {
        if (Status != ProductionOrderStatuses.InProduction)
        {
            throw InvalidTransition(ProductionOrderStatuses.InProduction);
        }

        var ingredient = _ingredients.FirstOrDefault(i => i.Id == ingredientId)
            ?? throw new BusinessException(ProductionErrorCodes.ProductionOrderIngredientNotFound)
                .WithData("IngredientId", ingredientId);
        ingredient.RecordActualCost(actualTotalCost);
        ActualIngredientCost = _ingredients.Sum(i => i.TotalCost);
        TotalProductionCost = ActualIngredientCost + LaborCost + OverheadCost;
        UnitProductionCost = TotalProductionCost / PlannedOutputQuantity;
    }

    public CompletionResult Complete(
        int actualOutputQuantity,
        int acceptedQuantity,
        int rejectedQuantity,
        DateTime? expiryDate,
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
        if (acceptedQuantity > 0 && !expiryDate.HasValue)
        {
            throw new BusinessException(ProductionErrorCodes.ProductionExpiryDateRequired);
        }
        if (rejectedQuantity > 0 && string.IsNullOrWhiteSpace(wasteReason))
        {
            throw new BusinessException(ProductionErrorCodes.WasteReasonRequired);
        }
        if (acceptedQuantity > 0 && expiryDate!.Value.Date < (ActualStartTime?.Date ?? DateTime.UtcNow.Date))
        {
            throw new BusinessException(ProductionErrorCodes.ProductionExpiryDateInPast)
                .WithData("ExpiryDate", expiryDate.Value.Date)
                .WithData("ProductionDate", ActualStartTime?.Date ?? DateTime.UtcNow.Date);
        }

        ActualOutputQuantity = actualOutputQuantity;
        AcceptedQuantity = acceptedQuantity;
        RejectedQuantity = rejectedQuantity;
        ExpiryDate = acceptedQuantity > 0 ? expiryDate!.Value.Date : null;
        WasteReason = rejectedQuantity > 0 ? wasteReason?.Trim() : null;
        CompletedByUserId = completedByUserId;
        CompletedAt = DateTime.UtcNow;
        Notes = notes;
        UnitProductionCost = acceptedQuantity > 0 ? TotalProductionCost / acceptedQuantity : 0m;
        Status = ProductionOrderStatuses.Completed;
        ExtraProperties[QualityStatusProperty] = acceptedQuantity > 0
            ? ProductionQualityStatuses.Pending
            : ProductionQualityStatuses.NotRequired;
        ExtraProperties.Remove(QualityReasonProperty);

        var releases = ReleaseAllocationExcess(AcceptedQuantity);
        return new CompletionResult(
            new OutputLine(FinishedProductId, AcceptedQuantity, UnitProductionCost, ExpiryDate),
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
        if (QualityStatus != ProductionQualityStatuses.Released
            && QualityStatus != ProductionQualityStatuses.NotRequired)
        {
            throw new BusinessException(ProductionErrorCodes.QualityReleaseRequiredForDispatch)
                .WithData("ProductionOrderId", Id)
                .WithData("QualityStatus", QualityStatus);
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

    public void HoldQuality(string reason, Guid? userId, DateTime at)
    {
        EnsureQualityTransition(
            [ProductionQualityStatuses.Pending, ProductionQualityStatuses.Released],
            ProductionQualityStatuses.Held,
            reason,
            userId,
            at);
    }

    public void ReleaseQuality(string? notes, Guid? userId, DateTime at)
    {
        EnsureQualityTransition(
            [ProductionQualityStatuses.Pending, ProductionQualityStatuses.Held],
            ProductionQualityStatuses.Released,
            notes,
            userId,
            at,
            reasonRequired: false);
    }

    public void SkipQualityRelease(Guid? userId, DateTime at)
    {
        EnsureQualityTransition(
            [ProductionQualityStatuses.Pending],
            ProductionQualityStatuses.NotRequired,
            reason: null,
            userId,
            at,
            reasonRequired: false);
    }

    public void RejectQuality(string reason, Guid? userId, DateTime at)
    {
        EnsureQualityTransition(
            [ProductionQualityStatuses.Pending, ProductionQualityStatuses.Held],
            ProductionQualityStatuses.Rejected,
            reason,
            userId,
            at);
    }

    private void EnsureQualityTransition(
        IReadOnlyCollection<string> allowedStatuses,
        string targetStatus,
        string? reason,
        Guid? userId,
        DateTime at,
        bool reasonRequired = true)
    {
        if (Status != ProductionOrderStatuses.Completed || !allowedStatuses.Contains(QualityStatus))
        {
            throw new BusinessException(ProductionErrorCodes.InvalidQualityTransition)
                .WithData("CurrentStatus", QualityStatus)
                .WithData("TargetStatus", targetStatus);
        }
        if (reasonRequired && string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessException(ProductionErrorCodes.QualityReasonRequired);
        }

        ExtraProperties[QualityStatusProperty] = targetStatus;
        ExtraProperties[QualityReasonProperty] = NormalizeOptional(reason);
        ExtraProperties[QualityUpdatedAtProperty] = at;
        ExtraProperties[QualityUpdatedByProperty] = userId;
    }

    private void EnsurePreStartState()
    {
        if (Status != ProductionOrderStatuses.Draft
            && Status != ProductionOrderStatuses.ReadyToCook
            && Status != ProductionOrderStatuses.WaitingForIngredients)
        {
            throw InvalidTransition(ProductionOrderStatuses.ReadyToCook);
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    public record OutputLine(Guid ProductId, int AcceptedQuantity, decimal UnitCost, DateTime? ExpiryDate);
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

    public record IngredientLotLine(
        Guid IngredientProductId,
        Guid BatchId,
        string BatchNumber,
        DateTime ExpiryDate,
        int Quantity,
        decimal UnitCost);
}
