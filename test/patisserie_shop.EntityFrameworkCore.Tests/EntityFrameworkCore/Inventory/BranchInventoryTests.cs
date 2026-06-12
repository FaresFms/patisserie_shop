using System;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Events;
using patisserie_shop.EntityFrameworkCore.Testing;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Inventory;

/// <summary>
/// The authoritative stock row: AdjustStockAsync must atomically raise the
/// StockChangedEto from the aggregate (with correct old/new quantities), write the
/// immutable AppStockMovement ledger row, and enforce the non-negative invariant.
/// </summary>
[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class BranchInventoryTests : IntegrationTestBase
{
    [Fact]
    public async Task AdjustStock_Raises_StockChangedEto_With_Old_And_New_Qty_And_Writes_The_Ledger_Row()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var dto = await InitializeInventoryAsync(branch.Id, product.Id, quantity: 50);

        var inventoryRepository = GetRequiredService<IRepository<AppBranchInventory, Guid>>();
        var manager = GetRequiredService<BranchInventoryManager>();

        await WithUnitOfWorkAsync(async () =>
        {
            var inventory = await inventoryRepository.GetAsync(dto.Id);

            var movement = await manager.AdjustStockAsync(
                inventory, newQuantity: 30, StockMovementTypes.ManualAdjustment);

            // Immutable ledger row carries the before/after snapshot and the delta.
            movement.QuantityBefore.ShouldBe(50);
            movement.QuantityAfter.ShouldBe(30);
            movement.Quantity.ShouldBe(-20);
            movement.MovementType.ShouldBe(StockMovementTypes.ManualAdjustment);

            // The event is raised from the aggregate itself (not from a service),
            // with the exact old/new pair the decision engine will evaluate.
            var eto = inventory.GetDistributedEvents()
                .Select(e => e.EventData)
                .OfType<StockChangedEto>()
                .ShouldHaveSingleItem();
            eto.BranchId.ShouldBe(branch.Id);
            eto.ProductId.ShouldBe(product.Id);
            eto.OldQty.ShouldBe(50);
            eto.NewQty.ShouldBe(30);

            inventory.QuantityOnHand.ShouldBe(30);
        });
    }

    [Fact]
    public async Task AdjustStock_Rejects_Negative_Quantities_And_Unknown_Movement_Types()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var dto = await InitializeInventoryAsync(branch.Id, product.Id, quantity: 10);

        var inventoryRepository = GetRequiredService<IRepository<AppBranchInventory, Guid>>();
        var manager = GetRequiredService<BranchInventoryManager>();

        await WithUnitOfWorkAsync(async () =>
        {
            var inventory = await inventoryRepository.GetAsync(dto.Id);

            (await Should.ThrowAsync<BusinessException>(() =>
                    manager.AdjustStockAsync(inventory, -1, StockMovementTypes.ManualAdjustment)))
                .Code.ShouldBe(InventoryErrorCodes.NegativeStock);

            (await Should.ThrowAsync<BusinessException>(() =>
                    manager.AdjustStockAsync(inventory, 5, "Shrinkage")))
                .Code.ShouldBe(InventoryErrorCodes.InvalidMovementType);

            inventory.QuantityOnHand.ShouldBe(10); // untouched by the failed calls
        });
    }

    [Fact]
    public async Task Initialize_Computes_Stock_Flags_And_Rejects_Duplicates_And_Invalid_Limits()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var manager = GetRequiredService<BranchInventoryManager>();

        // Flag semantics: low = qty ≤ minimum; out = qty ≤ 0.
        await WithUnitOfWorkAsync(async () =>
        {
            var atMinimum = await manager.InitializeAsync(branch.Id, product.Id, initialQuantity: 5, minimumStock: 5);
            atMinimum.IsLowStock.ShouldBeTrue();
            atMinimum.IsOutOfStock.ShouldBeFalse();
        });

        // min > max is rejected before anything is created.
        await WithUnitOfWorkAsync(async () =>
        {
            (await Should.ThrowAsync<BusinessException>(() =>
                    manager.InitializeAsync(branch.Id, Guid.NewGuid(), 0, minimumStock: 10, maximumStock: 5)))
                .Code.ShouldBe(InventoryErrorCodes.InvalidStockLimits);
        });

        // Zero on hand → both flags set (out-of-stock implies low-stock).
        var empty = await InitializeInventoryAsync(branch.Id, product.Id, quantity: 0, minimumStock: 5);
        empty.IsOutOfStock.ShouldBeTrue();
        empty.IsLowStock.ShouldBeTrue();

        // A second row for the same product+branch is rejected.
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            InitializeInventoryAsync(branch.Id, product.Id, quantity: 1));
        ex.Code.ShouldBe(InventoryErrorCodes.DuplicateBranchInventory);
    }
}
