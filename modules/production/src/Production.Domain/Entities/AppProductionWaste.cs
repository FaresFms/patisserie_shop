using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Production.Entities;

public class AppProductionWaste : CreationAuditedAggregateRoot<Guid>
{
    public Guid? ProductionOrderId { get; private set; }
    public Guid KitchenBranchId { get; private set; }
    public Guid ProductId { get; private set; }
    public string WasteType { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal TotalCost { get; private set; }
    public string Reason { get; private set; } = null!;
    public string? Notes { get; private set; }
    public Guid? RecordedByUserId { get; private set; }
    public DateTime RecordedAt { get; private set; }

    protected AppProductionWaste() { }

    internal AppProductionWaste(
        Guid id,
        Guid? productionOrderId,
        Guid kitchenBranchId,
        Guid productId,
        string wasteType,
        int quantity,
        decimal unitCost,
        string reason,
        Guid? recordedByUserId,
        DateTime recordedAt,
        string? notes = null)
        : base(id)
    {
        ProductionOrderId = productionOrderId;
        KitchenBranchId = kitchenBranchId;
        ProductId = productId;
        SetWasteType(wasteType);
        SetQuantity(quantity);
        SetUnitCost(unitCost);
        SetReason(reason);
        RecordedByUserId = recordedByUserId;
        RecordedAt = recordedAt;
        Notes = notes?.Trim();
    }

    private void SetWasteType(string wasteType)
    {
        if (!ProductionWasteTypes.IsValid(wasteType))
        {
            throw new BusinessException(ProductionErrorCodes.InvalidWasteType)
                .WithData("WasteType", wasteType ?? "(null)");
        }

        WasteType = wasteType;
    }

    private void SetQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidWasteQuantity)
                .WithData("Quantity", quantity);
        }

        Quantity = quantity;
        TotalCost = UnitCost * Quantity;
    }

    private void SetUnitCost(decimal unitCost)
    {
        if (unitCost < 0m)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidWasteCost)
                .WithData("UnitCost", unitCost);
        }

        UnitCost = unitCost;
        TotalCost = UnitCost * Quantity;
    }

    private void SetReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessException(ProductionErrorCodes.ProductionWasteReasonRequired);
        }
        if (!ProductionWasteReasons.IsValid(reason))
        {
            throw new BusinessException(ProductionErrorCodes.InvalidWasteReason)
                .WithData("Reason", reason);
        }

        Reason = reason;
    }
}
