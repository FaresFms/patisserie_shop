using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inventory;
using Inventory.Stocktakes;
using Operations.Sales;
using patisserie_shop.Analytics;
using patisserie_shop.EntityFrameworkCore.Testing;
using Shouldly;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Inventory;

[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class StocktakeReconciliationTests : IntegrationTestBase
{
    [Fact]
    public async Task Report_combines_recorded_sales_with_completed_stocktake_variances()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id, salePrice: 5m);
        var branch = await CreateBranchAsync();
        await InitializeInventoryAsync(branch.Id, product.Id, quantity: 10);

        await GetRequiredService<ISaleAppService>().CreateAsync(new CreateSaleDto
        {
            BranchId = branch.Id,
            SaleDate = DateTime.UtcNow,
            Currency = "USD",
            Items = new List<CreateSaleItemDto>
            {
                new() { ProductId = product.Id, Quantity = 2, UnitPrice = 5m }
            }
        });

        var stocktakes = GetRequiredService<IStocktakeSessionAppService>();
        var started = await stocktakes.StartAsync(new StartStocktakeSessionDto { BranchId = branch.Id });
        var submitted = await stocktakes.SubmitAsync(new SubmitStocktakeSessionDto
        {
            Id = started.Id,
            ConcurrencyStamp = started.ConcurrencyStamp,
            Notes = "Closing shelf count",
            Lines = started.Lines.ConvertAll(line => new StocktakeSessionDraftLineDto
            {
                LineId = line.Id,
                CountedQuantity = 7,
                Reason = StocktakeVarianceReasons.Damaged
            })
        });

        var pendingReport = await GetRequiredService<IStocktakeReconciliationAppService>()
            .GetAsync(new GetStocktakeReconciliationInput { Days = 7, BranchId = branch.Id });
        pendingReport.CompletedStocktakes.ShouldBe(0);
        pendingReport.ShortageQuantity.ShouldBe(0);

        await stocktakes.ApproveAsync(new ReviewStocktakeSessionDto
        {
            Id = submitted.Id,
            ConcurrencyStamp = submitted.ConcurrencyStamp
        });

        var second = await stocktakes.StartAsync(new StartStocktakeSessionDto { BranchId = branch.Id });
        var secondSubmitted = await stocktakes.SubmitAsync(new SubmitStocktakeSessionDto
        {
            Id = second.Id,
            ConcurrencyStamp = second.ConcurrencyStamp,
            Notes = "Second closing shelf count",
            Lines = second.Lines.ConvertAll(line => new StocktakeSessionDraftLineDto
            {
                LineId = line.Id,
                CountedQuantity = 6,
                Reason = StocktakeVarianceReasons.Damaged
            })
        });
        await stocktakes.ApproveAsync(new ReviewStocktakeSessionDto
        {
            Id = secondSubmitted.Id,
            ConcurrencyStamp = secondSubmitted.ConcurrencyStamp
        });

        var report = await GetRequiredService<IStocktakeReconciliationAppService>()
            .GetAsync(new GetStocktakeReconciliationInput { Days = 7, BranchId = branch.Id });

        report.TotalSales.ShouldBe(1);
        report.TotalRevenue.ShouldBe(10m);
        report.UnitsSold.ShouldBe(2);
        report.CurrentStockUnits.ShouldBe(6);
        report.CompletedStocktakes.ShouldBe(2);
        report.ProductsWithVariance.ShouldBe(1);
        report.ShortageQuantity.ShouldBe(2);
        report.OverageQuantity.ShouldBe(0);
        report.WriteOffQuantity.ShouldBe(2);
        report.UnrecordedSaleQuantity.ShouldBe(0);
        report.EstimatedShortageValue.ShouldBe(4m);
        report.EstimatedWriteOffValue.ShouldBe(4m);
        report.RepeatedVarianceProducts.ShouldBe(1);

        var row = report.Products.ShouldHaveSingleItem();
        row.ProductId.ShouldBe(product.Id);
        row.QuantitySold.ShouldBe(2);
        row.Revenue.ShouldBe(10m);
        row.CurrentStock.ShouldBe(6);
        row.StocktakeCount.ShouldBe(2);
        row.ShortageQuantity.ShouldBe(2);
        row.NetVariance.ShouldBe(-2);
        row.WriteOffQuantity.ShouldBe(2);
        row.HasRepeatedVariance.ShouldBeTrue();
        row.EstimatedShortageValue.ShouldBe(4m);
        row.ShortageRate.ShouldBe(50m);
        report.AttentionProducts.ShouldHaveSingleItem().ProductId.ShouldBe(product.Id);

        var reason = report.Reasons.ShouldHaveSingleItem();
        reason.Reason.ShouldBe(StocktakeVarianceReasons.Damaged);
        reason.ShortageQuantity.ShouldBe(2);
    }

    [Fact]
    public async Task Report_excludes_voided_sales_from_invoice_revenue_and_piece_totals()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id, salePrice: 4m);
        var branch = await CreateBranchAsync();
        await InitializeInventoryAsync(branch.Id, product.Id, quantity: 8);

        var sale = await GetRequiredService<ISaleAppService>().CreateAsync(new CreateSaleDto
        {
            BranchId = branch.Id,
            SaleDate = DateTime.UtcNow,
            Currency = "USD",
            Items = new List<CreateSaleItemDto>
            {
                new() { ProductId = product.Id, Quantity = 3, UnitPrice = 4m }
            }
        });

        var saleRepository = GetRequiredService<ISaleRepository>();
        await WithUnitOfWorkAsync(async () =>
        {
            var entity = await saleRepository.GetWithItemsAsync(sale.Id);
            entity.Void(Guid.NewGuid(), "Test void", DateTime.UtcNow);
            await saleRepository.UpdateAsync(entity, autoSave: true);
        });

        var report = await GetRequiredService<IStocktakeReconciliationAppService>()
            .GetAsync(new GetStocktakeReconciliationInput { Days = 7, BranchId = branch.Id });

        report.TotalSales.ShouldBe(0);
        report.TotalRevenue.ShouldBe(0m);
        report.UnitsSold.ShouldBe(0);
        report.Products.ShouldBeEmpty();
    }
}
