using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.StockBatches;
using Microsoft.Extensions.Logging;
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
        DateTime? batchExpiryDate = null)
    {
        var result = await AdjustStockDetailedAsync(
            inventory,
            newQuantity,
            movementType,
            notes,
            referenceId,
            referenceType,
            batchExpiryDate);

        return result.Movement;
    }

    public async Task<StockAdjustmentResult> AdjustStockDetailedAsync(
        AppBranchInventory inventory,
        int newQuantity,
        string movementType,
        string? notes = null,
        Guid? referenceId = null,
        string? referenceType = null,
        DateTime? batchExpiryDate = null)
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

        // Parallel best-effort batch (expiry) ledger. Runs AFTER the authoritative
        // stock mutation and may never fail it.
        var consumedBatches = await TrackBatchLedgerBestEffortAsync(
            inventory,
            delta,
            movementType,
            referenceId,
            batchExpiryDate);

        return new StockAdjustmentResult(movement, consumedBatches);
    }

    /// <summary>
    /// Keeps the FEFO batch ledger roughly in sync with the authoritative stock row:
    ///   delta &lt; 0 → consume |delta| across the product+branch's batches, FEFO
    ///                (non-expired earliest-expiry first, then expired oldest first);
    ///   delta &gt; 0 → if the product is perishable (ShelfLifeDays set), create ONE
    ///                batch of |delta| using the explicit expiry date when supplied,
    ///                otherwise expiring at utcToday + ShelfLifeDays.
    /// The whole block is try/catch-logged: batch bookkeeping is best-effort and a
    /// failure here must never roll back the stock operation.
    /// </summary>
    private async Task<IReadOnlyList<ConsumedStockBatchLine>> TrackBatchLedgerBestEffortAsync(
        AppBranchInventory inventory,
        int delta,
        string movementType,
        Guid? referenceId,
        DateTime? batchExpiryDate)
    {
        try
        {
            if (delta < 0)
            {
                // Waste movements clear EXPIRED batches first (oldest expiry first);
                // every other decrement uses normal FEFO (sellable, earliest-expiry first).
                return await _stockBatchManager.ConsumeFefoWithBreakdownAsync(
                    inventory.BranchId,
                    inventory.ProductId,
                    -delta,
                    expiredFirst: movementType == StockMovementTypes.WriteOff
                        || movementType == StockMovementTypes.ProductionWaste);
            }
            else if (delta > 0)
            {
                var product = await _productRepository.FindAsync(inventory.ProductId);
                if (product?.ShelfLifeDays is int shelfLifeDays)
                {
                    if (batchExpiryDate.HasValue)
                    {
                        await _stockBatchManager.CreateAsync(
                            inventory.BranchId,
                            inventory.ProductId,
                            delta,
                            batchExpiryDate.Value,
                            MapBatchSourceType(movementType),
                            referenceId);
                    }
                    else
                    {
                        await _stockBatchManager.ReceiveAsync(
                            inventory.BranchId,
                            inventory.ProductId,
                            delta,
                            shelfLifeDays,
                            MapBatchSourceType(movementType),
                            referenceId);
                    }
                }
            }

            return Array.Empty<ConsumedStockBatchLine>();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Stock batch ledger update failed for product {ProductId} at branch {BranchId} " +
                "(movement {MovementType}, delta {Delta}). The stock mutation itself is unaffected.",
                inventory.ProductId, inventory.BranchId, movementType, delta);

            return Array.Empty<ConsumedStockBatchLine>();
        }
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
