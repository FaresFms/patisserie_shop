using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Production.Entities;

public class AppProductionPlan : FullAuditedAggregateRoot<Guid>
{
    public string PlanNumber { get; private set; } = null!;
    public Guid KitchenBranchId { get; private set; }
    public DateTime ProductionDate { get; private set; }
    public string Status { get; private set; } = ProductionPlanStatuses.Draft;
    public Guid? CreatedByUserId { get; private set; }
    public Guid? ConfirmedByUserId { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<AppProductionPlanLine> _lines = new();
    public IReadOnlyCollection<AppProductionPlanLine> Lines => new ReadOnlyCollection<AppProductionPlanLine>(_lines);

    protected AppProductionPlan() { }

    internal AppProductionPlan(
        Guid id,
        string planNumber,
        Guid kitchenBranchId,
        DateTime productionDate,
        Guid? createdByUserId,
        string? notes = null)
        : base(id)
    {
        PlanNumber = Check.NotNullOrWhiteSpace(planNumber, nameof(planNumber), maxLength: 32);
        KitchenBranchId = kitchenBranchId;
        ProductionDate = productionDate.Date;
        CreatedByUserId = createdByUserId;
        Notes = notes;
        Status = ProductionPlanStatuses.Draft;
    }

    public AppProductionPlanLine AddLine(
        Guid lineId,
        Guid productId,
        int requestedQuantity,
        int forecastQuantity,
        int currentKitchenStock,
        int suggestedQuantity,
        int plannedQuantity,
        decimal estimatedIngredientCost,
        decimal estimatedLaborCost,
        decimal estimatedOverheadCost,
        decimal estimatedTotalCost)
    {
        EnsureDraft();

        if (_lines.Any(l => l.ProductId == productId))
        {
            throw new BusinessException(ProductionErrorCodes.DuplicateRequestProduct)
                .WithData("ProductId", productId);
        }

        var line = new AppProductionPlanLine(
            lineId,
            Id,
            productId,
            requestedQuantity,
            forecastQuantity,
            currentKitchenStock,
            suggestedQuantity,
            plannedQuantity,
            estimatedIngredientCost,
            estimatedLaborCost,
            estimatedOverheadCost,
            estimatedTotalCost);
        _lines.Add(line);
        return line;
    }

    public void UpdateLinePlannedQuantity(Guid lineId, int plannedQuantity, string? overrideReason)
    {
        EnsureDraft();
        FindLine(lineId).SetPlannedQuantity(plannedQuantity, overrideReason);
    }

    public void UpdateNotes(string? notes)
    {
        EnsureDraft();
        Notes = notes;
    }

    public void Confirm(Guid? confirmedByUserId)
    {
        if (Status != ProductionPlanStatuses.Draft)
        {
            throw InvalidTransition(ProductionPlanStatuses.Confirmed);
        }
        if (_lines.Count == 0 || _lines.All(l => l.PlannedQuantity <= 0))
        {
            throw new BusinessException(ProductionErrorCodes.CannotConfirmEmptyPlan);
        }

        ConfirmedByUserId = confirmedByUserId;
        ConfirmedAt = DateTime.UtcNow;
        Status = ProductionPlanStatuses.Confirmed;
    }

    public void MarkInProgress()
    {
        if (Status == ProductionPlanStatuses.InProgress)
        {
            return;
        }
        if (Status != ProductionPlanStatuses.Confirmed)
        {
            throw InvalidTransition(ProductionPlanStatuses.InProgress);
        }

        Status = ProductionPlanStatuses.InProgress;
    }

    public void Close()
    {
        if (Status == ProductionPlanStatuses.Closed)
        {
            return;
        }
        if (Status != ProductionPlanStatuses.Confirmed && Status != ProductionPlanStatuses.InProgress)
        {
            throw InvalidTransition(ProductionPlanStatuses.Closed);
        }

        Status = ProductionPlanStatuses.Closed;
    }

    public void Cancel(bool hasProductionOrders)
    {
        if (Status != ProductionPlanStatuses.Draft && Status != ProductionPlanStatuses.Confirmed)
        {
            throw InvalidTransition(ProductionPlanStatuses.Cancelled);
        }
        if (hasProductionOrders)
        {
            throw new BusinessException(ProductionErrorCodes.CannotCancelPlanWithOrders)
                .WithData("PlanId", Id);
        }

        Status = ProductionPlanStatuses.Cancelled;
    }

    private void EnsureDraft()
    {
        if (Status != ProductionPlanStatuses.Draft)
        {
            throw InvalidTransition(ProductionPlanStatuses.Draft);
        }
    }

    private AppProductionPlanLine FindLine(Guid lineId)
    {
        return _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new BusinessException(ProductionErrorCodes.PlanLineNotFound)
                .WithData("LineId", lineId);
    }

    private BusinessException InvalidTransition(string targetStatus) =>
        new BusinessException(ProductionErrorCodes.InvalidPlanStatusTransition)
            .WithData("CurrentStatus", Status)
            .WithData("TargetStatus", targetStatus);
}
