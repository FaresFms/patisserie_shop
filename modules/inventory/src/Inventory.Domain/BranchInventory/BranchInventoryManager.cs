using System;
using System.Threading.Tasks;
using Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Timing;

namespace Inventory.BranchInventory;

public class BranchInventoryManager : DomainService
{
    private readonly IRepository<AppBranchInventory, Guid> _inventoryRepository;
    private readonly IRepository<AppStockMovement, Guid> _movementRepository;

    public BranchInventoryManager(
        IRepository<AppBranchInventory, Guid> inventoryRepository,
        IRepository<AppStockMovement, Guid> movementRepository)
    {
        _inventoryRepository = inventoryRepository;
        _movementRepository = movementRepository;
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
        string? referenceType = null)
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
        if (delta > 0 && (movementType == StockMovementTypes.Purchase || movementType == StockMovementTypes.TransferIn))
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
        return movement;
    }

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
