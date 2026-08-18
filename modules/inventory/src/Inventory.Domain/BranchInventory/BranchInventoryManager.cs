using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.StockBatches;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Timing;

namespace Inventory.BranchInventory;

public class BranchInventoryManager : DomainService
{
    private readonly IRepository<AppBranchInventory, Guid> _inventoryRepository;
    private readonly IRepository<AppStockMovement, Guid> _movementRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly StockBatchManager _stockBatchManager;

    public BranchInventoryManager(
        IRepository<AppBranchInventory, Guid> inventoryRepository,
        IRepository<AppStockMovement, Guid> movementRepository,
        IRepository<AppProduct, Guid> productRepository,
        StockBatchManager stockBatchManager)
    {
        _inventoryRepository = inventoryRepository;
        _movementRepository = movementRepository;
        _productRepository = productRepository;
        _stockBatchManager = stockBatchManager;
    }

    public async Task<AppBranchInventory> InitializeAsync(
        Guid branchId,
        Guid productId,
        int initialQuantity = 0,
        int minimumStock = 0,
        int? maximumStock = null)
    {
        if (initialQuantity < 0)
        {
            throw new BusinessException(InventoryErrorCodes.NegativeStock);
        }
        ValidateLimits(minimumStock, maximumStock);

        var exists = await _inventoryRepository.AnyAsync(x => x.BranchId == branchId && x.ProductId == productId);
        if (exists)
        {
            throw new BusinessException(InventoryErrorCodes.DuplicateBranchInventory)
                .WithData("BranchId", branchId)
                .WithData("ProductId", productId);
        }

        return new AppBranchInventory(
            GuidGenerator.Create(),
            branchId,
            productId,
            initialQuantity,
            minimumStock,
            maximumStock);
    }

    /// <summary>
    /// Atomically updates QuantityOnHand on the aggregate (raises StockChangedEto)
    /// and records an immutable AppStockMovement ledger row.
    /// </summary>
    public async Task<AppStockMovement> AdjustStockAsync(
        AppBranchInventory inventory,
        int newQuantity,
        string movementType,
        string? notes = null,
        Guid? referenceId = null,
        string? referenceType = null,
        DateTime? batchExpiryDate = null,
        DateTime? batchProductionDate = null,
        decimal? batchUnitCost = null)
    {
        var result = await AdjustStockDetailedAsync(
            inventory,
            newQuantity,
            movementType,
            notes,
            referenceId,
            referenceType,
            batchExpiryDate,
            batchProductionDate,
            batchUnitCost);

        return result.Movement;
    }

    public async Task<StockAdjustmentResult> AdjustStockDetailedAsync(
        AppBranchInventory inventory,
        int newQuantity,
        string movementType,
        string? notes = null,
        Guid? referenceId = null,
        string? referenceType = null,
        DateTime? batchExpiryDate = null,
        DateTime? batchProductionDate = null,
        decimal? batchUnitCost = null)
    {
        Check.NotNull(inventory, nameof(inventory));

        if (!StockMovementTypes.IsValid(movementType))
        {
            throw new BusinessException(InventoryErrorCodes.InvalidMovementType)
                .WithData("MovementType", movementType ?? "null");
        }

        if (newQuantity < 0)
        {
            throw new BusinessException(InventoryErrorCodes.NegativeStock);
        }

        var quantityBefore = inventory.QuantityOnHand;
        var delta = newQuantity - quantityBefore;

        inventory.UpdateStock(newQuantity);

        var now = Clock.Now;
        if (delta > 0 && (movementType == StockMovementTypes.Purchase
            || movementType == StockMovementTypes.TransferIn
            || movementType == StockMovementTypes.ProductionOutput))
        {
            inventory.LastRestockedDate = now;
        }
        if (delta < 0 && movementType == StockMovementTypes.Sale)
        {
            inventory.LastSoldDate = now;
        }

        var movement = new AppStockMovement(
            GuidGenerator.Create(),
            inventory.BranchId,
            inventory.ProductId,
            movementType,
            delta,
            quantityBefore,
            newQuantity,
            referenceId,
            referenceType,
            notes);

        await _movementRepository.InsertAsync(movement);

        // Expiry bookkeeping is part of the same unit of work. Perishable stock may
        // not move unless the quantity is backed by usable batches.
        var consumedBatches = await TrackBatchLedgerAsync(
            inventory,
            delta,
            movementType,
            referenceId,
            batchExpiryDate,
            batchProductionDate,
            batchUnitCost);

        return new StockAdjustmentResult(movement, consumedBatches);
    }

    /// <summary>
    /// Keeps the FEFO batch ledger in sync with the authoritative stock row:
    ///   delta &lt; 0 → consume |delta| across the product+branch's batches. Sale,
    ///                production consumption and transfer-out never use expired lots;
    ///   delta &gt; 0 → if the product is perishable (ShelfLifeDays set), create ONE
    ///                batch of |delta| using the explicit expiry date when supplied,
    ///                else expiring at productionDate + ShelfLifeDays when the caller
    ///                knows when the goods were made, else utcToday + ShelfLifeDays.
    /// Perishable decrements require full batch coverage. A failure rolls back the
    /// surrounding unit of work so stock can never move without its expiry trail.
    /// </summary>
    private async Task<IReadOnlyList<ConsumedStockBatchLine>> TrackBatchLedgerAsync(
        AppBranchInventory inventory,
        int delta,
        string movementType,
        Guid? referenceId,
        DateTime? batchExpiryDate,
        DateTime? batchProductionDate,
        decimal? batchUnitCost)
    {
        var product = await _productRepository.FindAsync(inventory.ProductId);

        if (delta < 0)
        {
            // Waste movements clear EXPIRED batches first (oldest expiry first);
            // every other decrement uses usable FEFO batches first.
            var consumed = await _stockBatchManager.ConsumeFefoWithBreakdownAsync(
                inventory.BranchId,
                inventory.ProductId,
                -delta,
                expiredFirst: movementType == StockMovementTypes.WriteOff
                    || movementType == StockMovementTypes.ProductionWaste,
                includeExpiredFallback: movementType != StockMovementTypes.Sale
                    && movementType != StockMovementTypes.TransferOut
                    && movementType != StockMovementTypes.ProductionConsumption);

            if (product?.ShelfLifeDays.HasValue == true
                && consumed.Sum(x => x.Quantity) != -delta)
            {
                throw new BusinessException(InventoryErrorCodes.PerishableBatchCoverageRequired)
                    .WithData("ProductId", inventory.ProductId)
                    .WithData("Requested", -delta)
                    .WithData("Covered", consumed.Sum(x => x.Quantity));
            }

            return consumed;
        }

        if (delta > 0 && product?.ShelfLifeDays is int shelfLifeDays)
        {
            var resolvedUnitCost = batchUnitCost ?? product.CostPrice;
            batchExpiryDate ??= batchProductionDate?.Date.AddDays(shelfLifeDays);
            if (batchExpiryDate.HasValue)
            {
                await _stockBatchManager.CreateAsync(
                    inventory.BranchId,
                    inventory.ProductId,
                    delta,
                    batchExpiryDate.Value,
                    MapBatchSourceType(movementType),
                    referenceId,
                    resolvedUnitCost);
            }
            else if (movementType == StockMovementTypes.Purchase
                || movementType == StockMovementTypes.TransferIn
                || movementType == StockMovementTypes.ProductionOutput)
            {
                await _stockBatchManager.ReceiveAsync(
                    inventory.BranchId,
                    inventory.ProductId,
                    delta,
                    shelfLifeDays,
                    MapBatchSourceType(movementType),
                    referenceId,
                    resolvedUnitCost);
            }
        }

        return Array.Empty<ConsumedStockBatchLine>();
    }

    private static string MapBatchSourceType(string movementType) => movementType switch
    {
        StockMovementTypes.Purchase => StockBatchSourceTypes.Purchase,
        StockMovementTypes.TransferIn => StockBatchSourceTypes.TransferIn,
        StockMovementTypes.ProductionOutput => StockBatchSourceTypes.ProductionOutput,
        _ => StockBatchSourceTypes.Adjustment
    };

    public static void EnsureConcurrencyStamp(AppBranchInventory inventory, string? expectedStamp)
    {
        Check.NotNull(inventory, nameof(inventory));
        if (string.IsNullOrEmpty(expectedStamp))
        {
            return;
        }
        if (!string.Equals(inventory.ConcurrencyStamp, expectedStamp, StringComparison.Ordinal))
        {
            throw new BusinessException(InventoryErrorCodes.BranchInventoryConcurrency);
        }
    }

    public void UpdateLimits(AppBranchInventory inventory, int minimumStock, int? maximumStock)
    {
        Check.NotNull(inventory, nameof(inventory));
        ValidateLimits(minimumStock, maximumStock);
        inventory.MinimumStock = minimumStock;
        inventory.MaximumStock = maximumStock;
    }

    private static void ValidateLimits(int minimumStock, int? maximumStock)
    {
        if (minimumStock < 0 || (maximumStock.HasValue && maximumStock.Value < 0))
        {
            throw new BusinessException(InventoryErrorCodes.NegativeStock);
        }
        if (maximumStock.HasValue && maximumStock.Value < minimumStock)
        {
            throw new BusinessException(InventoryErrorCodes.InvalidStockLimits)
                .WithData("MinimumStock", minimumStock)
                .WithData("MaximumStock", maximumStock.Value);
        }
    }
}
