using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Timing;

namespace Inventory.BranchInventory;

/// <summary>
/// Validates and posts a complete branch stocktake. Validation is intentionally
/// separated from posting so submitted counts can be reviewed without changing stock.
/// </summary>
public class StocktakeManager : DomainService
{
    public const string MovementReferenceType = "Stocktake";

    private readonly IBranchInventoryRepository _inventoryRepository;
    private readonly BranchInventoryManager _inventoryManager;
    private readonly IClock _clock;

    public StocktakeManager(
        IBranchInventoryRepository inventoryRepository,
        BranchInventoryManager inventoryManager,
        IClock clock)
    {
        _inventoryRepository = inventoryRepository;
        _inventoryManager = inventoryManager;
        _clock = clock;
    }

    public async Task ValidateAsync(
        Guid branchId,
        IReadOnlyCollection<StocktakeCount> counts,
        string? notes)
    {
        await ValidateAndLoadAsync(branchId, counts, notes);
    }

    public async Task<StocktakePostingResult> PostAsync(
        Guid branchId,
        IReadOnlyCollection<StocktakeCount> counts,
        string? notes)
    {
        var validation = await ValidateAndLoadAsync(branchId, counts, notes);
        var referenceId = GuidGenerator.Create();
        var adjustedLineCount = 0;
        var matchedLineCount = 0;
        var writeOffLineCount = 0;
        var manualAdjustmentLineCount = 0;
        var shortageQuantity = 0;
        var overageQuantity = 0;

        foreach (var row in validation.Rows)
        {
            var count = validation.CountsByInventory[row.Inventory.Id];
            var difference = count.CountedQuantity - row.Inventory.QuantityOnHand;
            if (difference == 0)
            {
                matchedLineCount++;
                continue;
            }

            if (difference < 0) shortageQuantity += -difference;
            else overageQuantity += difference;

            DateTime? productionDate = difference > 0 && row.Product.ShelfLifeDays.HasValue
                ? count.ProductionDate!.Value.Date
                : null;
            var movementType = difference < 0 && StocktakeVarianceReasons.IsWriteOff(count.Reason!)
                ? StockMovementTypes.WriteOff
                : StockMovementTypes.ManualAdjustment;
            var movementNotes = StocktakeMovementNote.Format(
                count.Reason!,
                validation.Notes,
                count.ReasonNotes);

            await _inventoryManager.AdjustStockAsync(
                row.Inventory,
                count.CountedQuantity,
                movementType,
                movementNotes,
                referenceId,
                MovementReferenceType,
                batchProductionDate: productionDate);

            await _inventoryRepository.UpdateAsync(row.Inventory);
            adjustedLineCount++;
            if (movementType == StockMovementTypes.WriteOff) writeOffLineCount++;
            else manualAdjustmentLineCount++;
        }

        return new StocktakePostingResult(
            referenceId,
            counts.Count,
            adjustedLineCount,
            matchedLineCount,
            writeOffLineCount,
            manualAdjustmentLineCount,
            shortageQuantity,
            overageQuantity);
    }

    private async Task<StocktakeValidationContext> ValidateAndLoadAsync(
        Guid branchId,
        IReadOnlyCollection<StocktakeCount> counts,
        string? notes)
    {
        if (branchId == Guid.Empty)
        {
            throw new BusinessException(InventoryErrorCodes.BranchIdRequired);
        }

        if (counts.Count == 0)
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeEmpty);
        }

        notes = notes?.Trim();
        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeNotesRequired);
        }

        if (notes.Length > 300)
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeNotesTooLong);
        }

        var countsByInventory = new Dictionary<Guid, StocktakeCount>();
        foreach (var count in counts)
        {
            if (!countsByInventory.TryAdd(count.InventoryId, count))
            {
                throw new BusinessException(InventoryErrorCodes.StocktakeDuplicateLine)
                    .WithData("InventoryId", count.InventoryId);
            }

            if (count.CountedQuantity < 0)
            {
                throw new BusinessException(InventoryErrorCodes.NegativeStock);
            }

            if (count.ProductionDate?.Date > _clock.Now.Date)
            {
                throw new BusinessException(InventoryErrorCodes.StocktakeFutureProductionDate)
                    .WithData("InventoryId", count.InventoryId);
            }
        }

        var rows = await _inventoryRepository.GetStocktakeRowsAsync(branchId);
        if (rows.Count != counts.Count || rows.Any(row => !countsByInventory.ContainsKey(row.Inventory.Id)))
        {
            throw new BusinessException(InventoryErrorCodes.StocktakeIncomplete)
                .WithData("Expected", rows.Count)
                .WithData("Submitted", counts.Count);
        }

        foreach (var row in rows)
        {
            var count = countsByInventory[row.Inventory.Id];
            BranchInventoryManager.EnsureConcurrencyStamp(row.Inventory, count.ConcurrencyStamp);

            var difference = count.CountedQuantity - row.Inventory.QuantityOnHand;
            if (difference == 0) continue;

            if (string.IsNullOrWhiteSpace(count.Reason))
            {
                throw new BusinessException(InventoryErrorCodes.StocktakeReasonRequired)
                    .WithData("InventoryId", count.InventoryId);
            }

            if (!StocktakeVarianceReasons.IsAllowedForDifference(count.Reason, difference))
            {
                throw new BusinessException(InventoryErrorCodes.StocktakeInvalidReason)
                    .WithData("InventoryId", count.InventoryId)
                    .WithData("Reason", count.Reason);
            }

            if (count.Reason == StocktakeVarianceReasons.Other
                && string.IsNullOrWhiteSpace(count.ReasonNotes))
            {
                throw new BusinessException(InventoryErrorCodes.StocktakeOtherReasonRequired)
                    .WithData("InventoryId", count.InventoryId);
            }

            if (count.ReasonNotes?.Trim().Length > 120)
            {
                throw new BusinessException(InventoryErrorCodes.StocktakeMovementNoteTooLong)
                    .WithData("InventoryId", count.InventoryId);
            }

            if (difference > 0 && row.Product.ShelfLifeDays.HasValue && !count.ProductionDate.HasValue)
            {
                throw new BusinessException(InventoryErrorCodes.StocktakeProductionDateRequired)
                    .WithData("InventoryId", count.InventoryId)
                    .WithData("ProductId", row.Product.Id);
            }

            var movementNotes = StocktakeMovementNote.Format(count.Reason, notes, count.ReasonNotes);
            if (movementNotes.Length > 512)
            {
                throw new BusinessException(InventoryErrorCodes.StocktakeMovementNoteTooLong)
                    .WithData("InventoryId", count.InventoryId);
            }
        }

        return new StocktakeValidationContext(rows, countsByInventory, notes);
    }

    private sealed record StocktakeValidationContext(
        List<BranchInventoryWithProduct> Rows,
        Dictionary<Guid, StocktakeCount> CountsByInventory,
        string Notes);
}
