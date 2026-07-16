using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.StockBatches;
using Operations.Sales;
using Operations.StockTransfers;
using patisserie_shop.EntityFrameworkCore.Testing;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Inventory;

/// <summary>
/// Safety-critical FEFO coverage: usable expiry lots are required for perishable
/// sales/transfers, exact sale lots are restorable, and waste drains expired lots first.
/// </summary>
[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class StockBatchFefoTests : IntegrationTestBase
{
    private async Task<(Guid BranchId, Guid ProductId)> ArrangePerishableProductAsync(int onHand)
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id, shelfLifeDays: 10);
        var branch = await CreateBranchAsync();
        await InitializeInventoryAsync(branch.Id, product.Id, quantity: onHand);
        return (branch.Id, product.Id);
    }

    private async Task<AppStockBatch> CreateBatchAsync(Guid branchId, Guid productId, int quantity, int expiresInDays)
    {
        var manager = GetRequiredService<StockBatchManager>();
        return await WithUnitOfWorkAsync(() => manager.CreateAsync(
            branchId, productId, quantity,
            expiryDate: DateTime.UtcNow.Date.AddDays(expiresInDays),
            sourceType: StockBatchSourceTypes.Adjustment,
            autoSave: true));
    }

    private async Task RecordSaleAsync(Guid branchId, Guid productId, int quantity)
    {
        await GetRequiredService<ISaleAppService>().CreateAsync(new CreateSaleDto
        {
            BranchId = branchId,
            SaleDate = DateTime.UtcNow,
            Items = new List<CreateSaleItemDto>
            {
                new() { ProductId = productId, Quantity = quantity, UnitPrice = 5m }
            }
        });
    }

    private async Task<List<AppStockBatch>> LoadBatchesAsync(Guid productId)
    {
        var repository = GetRequiredService<IRepository<AppStockBatch, Guid>>();
        return await WithUnitOfWorkAsync(() => repository.GetListAsync(b => b.ProductId == productId));
    }

    private async Task<List<AppStockBatch>> LoadBatchesAsync(Guid branchId, Guid productId)
    {
        var repository = GetRequiredService<IRepository<AppStockBatch, Guid>>();
        return await WithUnitOfWorkAsync(() =>
            repository.GetListAsync(b => b.BranchId == branchId && b.ProductId == productId));
    }

    [Fact]
    public async Task A_Sale_Consumes_The_Earliest_Expiring_Batch_First()
    {
        var (branchId, productId) = await ArrangePerishableProductAsync(onHand: 10);

        // Insertion order is deliberately the REVERSE of expiry order.
        var later = await CreateBatchAsync(branchId, productId, quantity: 5, expiresInDays: 9);
        var earlier = await CreateBatchAsync(branchId, productId, quantity: 5, expiresInDays: 2);

        await RecordSaleAsync(branchId, productId, quantity: 7);

        var batches = await LoadBatchesAsync(productId);
        batches.Single(b => b.Id == earlier.Id).QuantityRemaining.ShouldBe(0); // drained first
        batches.Single(b => b.Id == later.Id).QuantityRemaining.ShouldBe(3);   // 5 − remaining 2
    }

    [Fact]
    public async Task Perishable_Sale_Is_Blocked_When_Usable_Batches_Do_Not_Cover_It()
    {
        // 20 on hand but the batch ledger only knows about 5 → 8 sold = 5 consumed + 3 drift.
        var (branchId, productId) = await ArrangePerishableProductAsync(onHand: 20);
        var only = await CreateBatchAsync(branchId, productId, quantity: 5, expiresInDays: 5);

        var exception = await Should.ThrowAsync<BusinessException>(
            () => RecordSaleAsync(branchId, productId, quantity: 8));
        exception.Code.ShouldBe(global::Operations.OperationsErrorCodes.SaleIncludesExpiredStock);

        var batch = (await LoadBatchesAsync(productId)).Single(b => b.Id == only.Id);
        batch.QuantityRemaining.ShouldBe(5);
        batch.IsDepleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Expired_Batch_Cannot_Be_Sold_And_Is_Left_Untouched()
    {
        var (branchId, productId) = await ArrangePerishableProductAsync(onHand: 5);
        var expired = await CreateBatchAsync(branchId, productId, quantity: 5, expiresInDays: -1);

        var exception = await Should.ThrowAsync<BusinessException>(
            () => RecordSaleAsync(branchId, productId, quantity: 1));
        exception.Code.ShouldBe(global::Operations.OperationsErrorCodes.SaleIncludesExpiredStock);

        var reloaded = (await LoadBatchesAsync(productId)).Single(b => b.Id == expired.Id);
        reloaded.QuantityRemaining.ShouldBe(5);
    }

    [Fact]
    public async Task Deleting_A_Sale_Restores_The_Exact_Expiry_Lots()
    {
        var (branchId, productId) = await ArrangePerishableProductAsync(onHand: 5);
        var expiry = DateTime.UtcNow.Date.AddDays(4);
        await CreateBatchAsync(branchId, productId, quantity: 5, expiresInDays: 4);
        var sales = GetRequiredService<ISaleAppService>();

        var sale = await sales.CreateAsync(new CreateSaleDto
        {
            BranchId = branchId,
            SaleDate = DateTime.UtcNow,
            Items = new List<CreateSaleItemDto>
            {
                new() { ProductId = productId, Quantity = 3, UnitPrice = 5m }
            }
        });

        await sales.DeleteAsync(sale.Id);

        var batches = await LoadBatchesAsync(branchId, productId);
        batches.Where(b => b.ExpiryDate == expiry).Sum(b => b.QuantityRemaining).ShouldBe(5);
    }

    [Fact]
    public async Task Production_Waste_Consumes_Expired_Batches_First()
    {
        var (branchId, productId) = await ArrangePerishableProductAsync(onHand: 10);
        var fresh = await CreateBatchAsync(branchId, productId, quantity: 6, expiresInDays: 5);
        var expired = await CreateBatchAsync(branchId, productId, quantity: 4, expiresInDays: -2);
        var inventoryRepository = GetRequiredService<IRepository<AppBranchInventory, Guid>>();
        var inventory = await WithUnitOfWorkAsync(async () =>
            (await inventoryRepository.GetListAsync(i => i.BranchId == branchId && i.ProductId == productId)).Single());
        var inventoryService = GetRequiredService<IBranchInventoryAppService>();

        await inventoryService.AdjustStockAsync(inventory.Id, new AdjustStockDto
        {
            NewQuantity = 6,
            MovementType = StockMovementTypes.ProductionWaste,
            Notes = "Expired finished goods before dispatch"
        });

        var batches = await LoadBatchesAsync(branchId, productId);
        batches.Single(b => b.Id == expired.Id).QuantityRemaining.ShouldBe(0);
        batches.Single(b => b.Id == fresh.Id).QuantityRemaining.ShouldBe(6);
    }

    [Fact]
    public async Task ConsumeFefo_WithBreakdown_Returns_The_Source_Expiry_Splits()
    {
        var (branchId, productId) = await ArrangePerishableProductAsync(onHand: 10);
        var later = await CreateBatchAsync(branchId, productId, quantity: 5, expiresInDays: 9);
        var earlier = await CreateBatchAsync(branchId, productId, quantity: 5, expiresInDays: 2);
        var manager = GetRequiredService<StockBatchManager>();

        var consumed = await WithUnitOfWorkAsync(() =>
            manager.ConsumeFefoWithBreakdownAsync(branchId, productId, quantity: 7));

        consumed.Count.ShouldBe(2);
        consumed[0].BatchId.ShouldBe(earlier.Id);
        consumed[0].ExpiryDate.ShouldBe(earlier.ExpiryDate);
        consumed[0].Quantity.ShouldBe(5);
        consumed[1].BatchId.ShouldBe(later.Id);
        consumed[1].ExpiryDate.ShouldBe(later.ExpiryDate);
        consumed[1].Quantity.ShouldBe(2);
    }

    [Fact]
    public async Task Transfer_Completion_Carries_Source_Batch_Expiry_To_The_Destination()
    {
        var (sourceBranchId, productId) = await ArrangePerishableProductAsync(onHand: 10);
        var destinationBranch = await CreateBranchAsync();
        var later = await CreateBatchAsync(sourceBranchId, productId, quantity: 5, expiresInDays: 9);
        var earlier = await CreateBatchAsync(sourceBranchId, productId, quantity: 5, expiresInDays: 2);
        var transfers = GetRequiredService<IStockTransferAppService>();

        var transfer = await transfers.CreateAsync(new CreateStockTransferDto
        {
            FromBranchId = sourceBranchId,
            ToBranchId = destinationBranch.Id,
            RequestedDate = DateTime.UtcNow,
            Notes = "Expiry-preserving transfer test"
        });
        var item = await transfers.AddItemAsync(transfer.Id, new AddStockTransferItemDto
        {
            ProductId = productId,
            RequestedQuantity = 7
        });

        await transfers.SubmitAsync(transfer.Id);
        await transfers.ApproveAsync(transfer.Id);
        await transfers.ShipAsync(transfer.Id, new ShipStockTransferDto());
        await transfers.CompleteAsync(transfer.Id, new CompleteStockTransferDto
        {
            Lines = new List<CompleteTransferLineDto>
            {
                new() { ItemId = item.Id, TransferredQuantity = 7 }
            }
        });

        var destinationBatches = await LoadBatchesAsync(destinationBranch.Id, productId);
        destinationBatches.Count.ShouldBe(2);
        destinationBatches.Single(b => b.ExpiryDate == earlier.ExpiryDate).QuantityRemaining.ShouldBe(5);
        destinationBatches.Single(b => b.ExpiryDate == later.ExpiryDate).QuantityRemaining.ShouldBe(2);
    }

    [Fact]
    public async Task Transfer_Cannot_Ship_Expired_Perishable_Stock()
    {
        var (sourceBranchId, productId) = await ArrangePerishableProductAsync(onHand: 5);
        var destinationBranch = await CreateBranchAsync();
        var expired = await CreateBatchAsync(sourceBranchId, productId, quantity: 5, expiresInDays: -1);
        var transfers = GetRequiredService<IStockTransferAppService>();

        var transfer = await transfers.CreateAsync(new CreateStockTransferDto
        {
            FromBranchId = sourceBranchId,
            ToBranchId = destinationBranch.Id,
            RequestedDate = DateTime.UtcNow
        });
        await transfers.AddItemAsync(transfer.Id, new AddStockTransferItemDto
        {
            ProductId = productId,
            RequestedQuantity = 1
        });
        await transfers.SubmitAsync(transfer.Id);
        await transfers.ApproveAsync(transfer.Id);

        var exception = await Should.ThrowAsync<BusinessException>(
            () => transfers.ShipAsync(transfer.Id, new ShipStockTransferDto()));
        exception.Code.ShouldBe(global::Operations.OperationsErrorCodes.InsufficientStockAtSource);

        var reloaded = (await LoadBatchesAsync(sourceBranchId, productId)).Single(b => b.Id == expired.Id);
        reloaded.QuantityRemaining.ShouldBe(5);
    }

    [Fact]
    public async Task Batch_Consume_Validates_Quantity_And_Expiry_Is_Date_Based()
    {
        var (branchId, productId) = await ArrangePerishableProductAsync(onHand: 0);
        var batch = await CreateBatchAsync(branchId, productId, quantity: 5, expiresInDays: 0);

        // Zero, negative and over-consumption are rejected; the caller must split instead.
        Should.Throw<BusinessException>(() => batch.Consume(0))
            .Code.ShouldBe(InventoryErrorCodes.InvalidBatchConsumeQuantity);
        Should.Throw<BusinessException>(() => batch.Consume(-1))
            .Code.ShouldBe(InventoryErrorCodes.InvalidBatchConsumeQuantity);
        Should.Throw<BusinessException>(() => batch.Consume(6))
            .Code.ShouldBe(InventoryErrorCodes.InvalidBatchConsumeQuantity);

        batch.Consume(5);
        batch.IsDepleted.ShouldBeTrue();

        // The expiry day itself is still sellable; only the day after counts as expired.
        var today = DateTime.UtcNow.Date;
        batch.IsExpired(today).ShouldBeFalse();           // expires today → not expired yet
        batch.IsExpired(today.AddDays(1)).ShouldBeTrue(); // one day later → expired
    }
}
