using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Inventory.Entities;

public class AppStocktakeLine : Entity<Guid>
{
    public Guid SessionId { get; private set; }
    public Guid InventoryId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public string ProductSku { get; private set; } = null!;
    public string ProductUnit { get; private set; } = null!;
    public int ExpectedQuantity { get; private set; }
    public bool IsPerishable { get; private set; }
    public string InventoryConcurrencyStamp { get; private set; } = null!;
    public int? CountedQuantity { get; private set; }
    public string? Reason { get; private set; }
    public string? ReasonNotes { get; private set; }
    public DateTime? ProductionDate { get; private set; }

    public int? Difference => CountedQuantity - ExpectedQuantity;

    protected AppStocktakeLine()
    {
    }

    internal AppStocktakeLine(
        Guid id,
        Guid sessionId,
        Guid inventoryId,
        Guid productId,
        string productName,
        string productSku,
        string productUnit,
        int expectedQuantity,
        bool isPerishable,
        string inventoryConcurrencyStamp)
        : base(id)
    {
        SessionId = sessionId;
        InventoryId = inventoryId;
        ProductId = productId;
        ProductName = Check.NotNullOrWhiteSpace(productName, nameof(productName), 128);
        ProductSku = Check.NotNullOrWhiteSpace(productSku, nameof(productSku), 64);
        ProductUnit = Check.NotNullOrWhiteSpace(productUnit, nameof(productUnit), 32);
        ExpectedQuantity = expectedQuantity >= 0
            ? expectedQuantity
            : throw new BusinessException(InventoryErrorCodes.NegativeStock);
        IsPerishable = isPerishable;
        InventoryConcurrencyStamp = Check.NotNullOrWhiteSpace(
            inventoryConcurrencyStamp,
            nameof(inventoryConcurrencyStamp),
            40);
    }

    internal void UpdateDraft(
        int? countedQuantity,
        string? reason,
        string? reasonNotes,
        DateTime? productionDate,
        DateTime today)
    {
        if (countedQuantity < 0)
        {
            throw new BusinessException(InventoryErrorCodes.NegativeStock);
        }

        reason = reason?.Trim();
        reasonNotes = reasonNotes?.Trim();
        if (reasonNotes?.Length > 120)
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeMovementNoteTooLong);
        }

        if (productionDate?.Date > today.Date)
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeFutureProductionDate);
        }

        CountedQuantity = countedQuantity;
        if (!countedQuantity.HasValue || countedQuantity.Value == ExpectedQuantity)
        {
            Reason = null;
            ReasonNotes = null;
            ProductionDate = null;
            return;
        }

        var difference = countedQuantity.Value - ExpectedQuantity;
        if (!string.IsNullOrWhiteSpace(reason)
            && !StocktakeVarianceReasons.IsAllowedForDifference(reason, difference))
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeInvalidReason);
        }

        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
        ReasonNotes = string.IsNullOrWhiteSpace(reasonNotes) ? null : reasonNotes;
        ProductionDate = IsPerishable && difference > 0 ? productionDate?.Date : null;
    }
}
