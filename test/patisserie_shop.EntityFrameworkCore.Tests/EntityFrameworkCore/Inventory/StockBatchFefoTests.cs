using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.Entities;
using Inventory.StockBatches;
using Operations.Sales;
using patisserie_shop.EntityFrameworkCore.Testing;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Inventory;

/// <summary>
/// The best-effort FEFO (first-expired-first-out) batch ledger: consumption order,
/// drift tolerance (a shortfall must never fail the authoritative stock mutation),
/// and the AppStockBatch consumption invariants.
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
    public async Task Consumption_Beyond_Batch_Coverage_Never_Fails_The_Sale()
    {
        // 20 on hand but the batch ledger only knows about 5 → 8 sold = 5 consumed + 3 drift.
        var (branchId, productId) = await ArrangePerishableProductAsync(onHand: 20);
        var only = await CreateBatchAsync(branchId, productId, quantity: 5, expiresInDays: 5);

        await RecordSaleAsync(branchId, productId, quantity: 8); // must NOT throw

        var batch = (await LoadBatchesAsync(productId)).Single(b => b.Id == only.Id);
        batch.QuantityRemaining.ShouldBe(0);
        batch.IsDepleted.ShouldBeTrue();
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
