using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

public class AppStockMovement : CreationAuditedAggregateRoot<Guid>
{
    public Guid BranchId { get; }
    public Guid ProductId { get; }
    public string MovementType { get; } = null!;
    public int Quantity { get; }
    public int QuantityBefore { get; }
    public int QuantityAfter { get; }
    public Guid? ReferenceId { get; }
    public string? ReferenceType { get; }
    public string? Notes { get; }

    protected AppStockMovement() { }

    public AppStockMovement(
        Guid id,
        Guid branchId,
        Guid productId,
        string movementType,
        int quantity,
        int quantityBefore,
        int quantityAfter,
        Guid? referenceId = null,
        string? referenceType = null,
        string? notes = null)
        : base(id)
    {
        BranchId = branchId;
        ProductId = productId;
        MovementType = movementType;
        Quantity = quantity;
        QuantityBefore = quantityBefore;
        QuantityAfter = quantityAfter;
        ReferenceId = referenceId;
        ReferenceType = referenceType;
        Notes = notes;
    }
}
