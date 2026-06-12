using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Intelligence.Entities;
using Operations.Sales;
using patisserie_shop.EntityFrameworkCore.Testing;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Intelligence;

/// <summary>
/// The nightly demand-velocity computation: trailing 7/30-day averages from real
/// sale rows, and the global ABC classification by cumulative 30-day revenue share
/// (≤80% → A, ≤95% → B, else C).
/// </summary>
[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class VelocityScannerTests : IntegrationTestBase
{
    private async Task RecordSaleAsync(Guid branchId, Guid productId, int quantity, decimal unitPrice, int daysAgo)
    {
        await GetRequiredService<ISaleAppService>().CreateAsync(new CreateSaleDto
        {
            BranchId = branchId,
            SaleDate = DateTime.UtcNow.AddDays(-daysAgo),
            Items = new List<CreateSaleItemDto>
            {
                new() { ProductId = productId, Quantity = quantity, UnitPrice = unitPrice }
            }
        });
    }

    private async Task<List<AppProductVelocity>> ScanAndLoadAsync()
    {
        var scanner = GetRequiredService<VelocityScannerService>();
        await WithUnitOfWorkAsync(() => scanner.ScanAsync());

        var repository = GetRequiredService<IRepository<AppProductVelocity, Guid>>();
        return await WithUnitOfWorkAsync(() => repository.GetListAsync());
    }

    [Fact]
    public async Task Scan_Computes_The_Trailing_7_And_30_Day_Averages_Per_Product_And_Branch()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        await InitializeInventoryAsync(branch.Id, product.Id, quantity: 200);

        await RecordSaleAsync(branch.Id, product.Id, quantity: 14, unitPrice: 5m, daysAgo: 2);  // inside both windows
        await RecordSaleAsync(branch.Id, product.Id, quantity: 16, unitPrice: 5m, daysAgo: 20); // 30-day window only

        var rows = await ScanAndLoadAsync();

        var row = rows.ShouldHaveSingleItem();
        row.ProductId.ShouldBe(product.Id);
        row.BranchId.ShouldBe(branch.Id);
        row.AvgDailySales7.ShouldBe(2m);            // 14 ÷ 7
        row.AvgDailySales30.ShouldBe(1m);           // (14 + 16) ÷ 30
        row.QuantitySold30.ShouldBe(30);
        row.Revenue30.ShouldBe(150m);               // 30 × 5
        row.ComputedAtUtc.ShouldNotBe(default);
    }

    [Fact]
    public async Task Scan_Classifies_Products_By_Cumulative_Revenue_Share()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var branch = await CreateBranchAsync();

        // Revenue split 80 / 15 / 5 → cumulative shares 0.80 (A), 0.95 (B), 1.00 (C).
        var bestSeller = await CreateProductAsync(category.Id);
        var midSeller = await CreateProductAsync(category.Id);
        var slowSeller = await CreateProductAsync(category.Id);
        foreach (var p in new[] { bestSeller, midSeller, slowSeller })
        {
            await InitializeInventoryAsync(branch.Id, p.Id, quantity: 500);
        }

        await RecordSaleAsync(branch.Id, bestSeller.Id, quantity: 80, unitPrice: 1m, daysAgo: 5);
        await RecordSaleAsync(branch.Id, midSeller.Id, quantity: 15, unitPrice: 1m, daysAgo: 5);
        await RecordSaleAsync(branch.Id, slowSeller.Id, quantity: 5, unitPrice: 1m, daysAgo: 5);

        var rows = await ScanAndLoadAsync();

        rows.Single(r => r.ProductId == bestSeller.Id).AbcClass.ShouldBe("A");
        rows.Single(r => r.ProductId == midSeller.Id).AbcClass.ShouldBe("B");
        rows.Single(r => r.ProductId == slowSeller.Id).AbcClass.ShouldBe("C");
    }
}
