using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Entities;
using patisserie_shop.EntityFrameworkCore.Testing;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Inventory;

[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class StocktakeTests : IntegrationTestBase
{
    [Fact]
    public async Task Post_updates_differences_and_groups_movements_under_one_reference()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product1 = await CreateProductAsync(category.Id);
        var product2 = await CreateProductAsync(category.Id);
        var product3 = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory1 = await InitializeInventoryAsync(branch.Id, product1.Id, quantity: 10);
        var inventory2 = await InitializeInventoryAsync(branch.Id, product2.Id, quantity: 4);
        var inventory3 = await InitializeInventoryAsync(branch.Id, product3.Id, quantity: 8);

        var manager = GetRequiredService<StocktakeManager>();
        StocktakePostingResult result = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            result = await manager.PostAsync(
                branch.Id,
                new List<StocktakeCount>
                {
                    new(inventory1.Id, 7, inventory1.ConcurrencyStamp, StocktakeVarianceReasons.Damaged, null, null),
                    new(inventory2.Id, 9, inventory2.ConcurrencyStamp, StocktakeVarianceReasons.UnrecordedReceipt, null, null),
                    new(inventory3.Id, 8, inventory3.ConcurrencyStamp, null, null, null)
                },
                "End-of-day shelf count");
        });

        result.CountedLineCount.ShouldBe(3);
        result.AdjustedLineCount.ShouldBe(2);
        result.MatchedLineCount.ShouldBe(1);
        result.WriteOffLineCount.ShouldBe(1);
        result.ManualAdjustmentLineCount.ShouldBe(1);
        result.ShortageQuantity.ShouldBe(3);
        result.OverageQuantity.ShouldBe(5);

        var inventoryRepository = GetRequiredService<IRepository<AppBranchInventory, Guid>>();
        (await inventoryRepository.GetAsync(inventory1.Id)).QuantityOnHand.ShouldBe(7);
        (await inventoryRepository.GetAsync(inventory2.Id)).QuantityOnHand.ShouldBe(9);
        (await inventoryRepository.GetAsync(inventory3.Id)).QuantityOnHand.ShouldBe(8);

        var movementRepository = GetRequiredService<IRepository<AppStockMovement, Guid>>();
        var movements = await movementRepository.GetListAsync(movement => movement.ReferenceId == result.ReferenceId);
        movements.Count.ShouldBe(2);
        movements.ShouldAllBe(movement => movement.ReferenceType == StocktakeManager.MovementReferenceType);
        movements.Count(movement => movement.MovementType == StockMovementTypes.WriteOff).ShouldBe(1);
        movements.Count(movement => movement.MovementType == StockMovementTypes.ManualAdjustment).ShouldBe(1);
        foreach (var movement in movements)
        {
            StocktakeMovementNote.TryParse(movement.Notes, out _, out var text).ShouldBeTrue();
            text.ShouldBe("End-of-day shelf count");
        }
        movements.Sum(movement => movement.Quantity).ShouldBe(2);
    }

    [Fact]
    public async Task Post_rejects_an_incomplete_sheet_before_changing_stock()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product1 = await CreateProductAsync(category.Id);
        var product2 = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory1 = await InitializeInventoryAsync(branch.Id, product1.Id, quantity: 10);
        await InitializeInventoryAsync(branch.Id, product2.Id, quantity: 5);
        var manager = GetRequiredService<StocktakeManager>();

        var exception = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
            manager.PostAsync(
                branch.Id,
                new List<StocktakeCount>
                {
                    new(inventory1.Id, 7, inventory1.ConcurrencyStamp, StocktakeVarianceReasons.CountingError, null, null)
                },
                "Incomplete count")));

        exception.Code.ShouldBe(InventoryErrorCodes.StocktakeIncomplete);

        var inventoryRepository = GetRequiredService<IRepository<AppBranchInventory, Guid>>();
        (await inventoryRepository.GetAsync(inventory1.Id)).QuantityOnHand.ShouldBe(10);
    }

    [Fact]
    public async Task Post_rejects_a_stale_sheet_and_rolls_back_every_line()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product1 = await CreateProductAsync(category.Id);
        var product2 = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory1 = await InitializeInventoryAsync(branch.Id, product1.Id, quantity: 10);
        var inventory2 = await InitializeInventoryAsync(branch.Id, product2.Id, quantity: 5);

        // Simulates a sale or another adjustment after the stocktake sheet was loaded.
        await AdjustStockAsync(inventory2.Id, newQuantity: 4);

        var manager = GetRequiredService<StocktakeManager>();
        var exception = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
            manager.PostAsync(
                branch.Id,
                new List<StocktakeCount>
                {
                    new(inventory1.Id, 7, inventory1.ConcurrencyStamp, StocktakeVarianceReasons.CountingError, null, null),
                    new(inventory2.Id, 4, inventory2.ConcurrencyStamp, null, null, null)
                },
                "Stale count")));

        exception.Code.ShouldBe(InventoryErrorCodes.BranchInventoryConcurrency);

        var inventoryRepository = GetRequiredService<IRepository<AppBranchInventory, Guid>>();
        (await inventoryRepository.GetAsync(inventory1.Id)).QuantityOnHand.ShouldBe(10);
        (await inventoryRepository.GetAsync(inventory2.Id)).QuantityOnHand.ShouldBe(4);

        var movementRepository = GetRequiredService<IRepository<AppStockMovement, Guid>>();
        (await movementRepository.GetListAsync(movement =>
            movement.Notes != null && movement.Notes.Contains("Stale count"))).ShouldBeEmpty();
    }

    [Fact]
    public async Task Post_requires_a_reason_for_every_difference()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory = await InitializeInventoryAsync(branch.Id, product.Id, quantity: 10);
        var manager = GetRequiredService<StocktakeManager>();

        var exception = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
            manager.PostAsync(
                branch.Id,
                new List<StocktakeCount>
                {
                    new(inventory.Id, 9, inventory.ConcurrencyStamp, null, null, null)
                },
                "Missing reason")));

        exception.Code.ShouldBe(InventoryErrorCodes.StocktakeReasonRequired);
    }

    [Fact]
    public async Task Other_reason_requires_a_short_explanation()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory = await InitializeInventoryAsync(branch.Id, product.Id, quantity: 10);
        var manager = GetRequiredService<StocktakeManager>();

        var exception = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
            manager.PostAsync(
                branch.Id,
                new List<StocktakeCount>
                {
                    new(inventory.Id, 11, inventory.ConcurrencyStamp, StocktakeVarianceReasons.Other, null, null)
                },
                "Other count")));

        exception.Code.ShouldBe(InventoryErrorCodes.StocktakeOtherReasonRequired);
    }

    [Fact]
    public async Task Complimentary_shortage_is_a_manual_adjustment_not_a_write_off()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory = await InitializeInventoryAsync(branch.Id, product.Id, quantity: 10);
        var manager = GetRequiredService<StocktakeManager>();
        StocktakePostingResult result = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            result = await manager.PostAsync(
                branch.Id,
                new List<StocktakeCount>
                {
                    new(inventory.Id, 9, inventory.ConcurrencyStamp, StocktakeVarianceReasons.Complimentary, null, null)
                },
                "Complimentary counter piece");
        });

        result.WriteOffLineCount.ShouldBe(0);
        result.ManualAdjustmentLineCount.ShouldBe(1);

        var movementRepository = GetRequiredService<IRepository<AppStockMovement, Guid>>();
        var movement = (await movementRepository.GetListAsync(item => item.ReferenceId == result.ReferenceId))
            .ShouldHaveSingleItem();
        movement.MovementType.ShouldBe(StockMovementTypes.ManualAdjustment);
    }
}
