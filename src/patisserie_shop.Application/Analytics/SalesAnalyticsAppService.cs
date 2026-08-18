using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
using Microsoft.AspNetCore.Authorization;
using Operations.Permissions;
using Operations.Sales;
using Volo.Abp.Domain.Repositories;

namespace patisserie_shop.Analytics;

/// <summary>
/// Composes Operations sales aggregates with Inventory product/category names
/// in the host app — same cross-module, in-memory composition pattern as
/// <see cref="patisserie_shop.Dashboard.AdminDashboardAppService"/>. All heavy
/// grouping happens in <see cref="ISaleRepository"/>; this service only maps
/// repository read-models onto DTOs.
/// </summary>
[Authorize(OperationsPermissions.Sales.Default)]
public class SalesAnalyticsAppService : patisserie_shopAppService, ISalesAnalyticsAppService
{
    private const int MoversLimit = 10;

    private readonly ISaleRepository _saleRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppCategory, Guid> _categoryRepository;

    public SalesAnalyticsAppService(
        ISaleRepository saleRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppCategory, Guid> categoryRepository)
    {
        _saleRepository = saleRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<SalesAnalyticsDto> GetAsync(GetSalesAnalyticsInput input)
    {
        var days = NormalizeDays(input.Days);

        var todayStart = DateTime.UtcNow.Date;
        var fromUtc = todayStart.AddDays(-(days - 1));
        var toUtc = todayStart.AddDays(1);

        var branchScope = await ResolveBranchScopeAsync(input.BranchId);

        var daily = await _saleRepository.GetDailySalesAsync(fromUtc, toUtc, branchScope);
        var byBranch = await _saleRepository.GetSalesByBranchAsync(fromUtc, toUtc, branchScope);
        var productTotals = await _saleRepository.GetProductSalesTotalsAsync(fromUtc, toUtc, branchScope);

        // Reference data for display names (small sets — same in-memory
        // dictionary composition the dashboards use).
        var branchNames = (await _branchRepository.GetListAsync())
            .ToDictionary(b => b.Id, b => b.DisplayName);
        var productLookup = (await _productRepository.GetListAsync())
            .ToDictionary(p => p.Id, p => p);
        var categoryNames = (await _categoryRepository.GetListAsync())
            .ToDictionary(c => c.Id, c => c.DisplayName);

        var totalRevenue = daily.Sum(d => d.TotalAmount);
        var totalSales = daily.Sum(d => d.SaleCount);

        // Daily series with zero-filled gaps so the chart axis is continuous.
        var dailyMap = daily.ToDictionary(d => d.Date.Date, d => d);
        var series = new List<DailySalesPointDto>(days);
        for (var i = days - 1; i >= 0; i--)
        {
            var day = todayStart.AddDays(-i);
            dailyMap.TryGetValue(day, out var agg);
            series.Add(new DailySalesPointDto
            {
                Date = day,
                Revenue = agg?.TotalAmount ?? 0m,
                SaleCount = agg?.SaleCount ?? 0
            });
        }

        var branchSlices = byBranch
            .Select(b => new BranchSalesSliceDto
            {
                BranchId = b.BranchId,
                BranchName = branchNames.GetValueOrDefault(b.BranchId, "(deleted branch)"),
                Revenue = b.TotalAmount,
                SaleCount = b.SaleCount
            })
            .OrderByDescending(b => b.Revenue)
            .ToList();

        // Category mix: productId → categoryId → name, summed in memory.
        var categoryRevenue = new Dictionary<Guid, decimal>();
        foreach (var p in productTotals)
        {
            var categoryId = productLookup.TryGetValue(p.ProductId, out var product)
                ? product.CategoryId
                : Guid.Empty;
            categoryRevenue.TryGetValue(categoryId, out var current);
            categoryRevenue[categoryId] = current + p.TotalRevenue;
        }

        var categoryMix = categoryRevenue
            .Select(kv => new CategorySalesSliceDto
            {
                CategoryId = kv.Key,
                CategoryName = kv.Key == Guid.Empty
                    ? "(uncategorized)"
                    : categoryNames.GetValueOrDefault(kv.Key, "(deleted category)"),
                Revenue = kv.Value
            })
            .OrderByDescending(c => c.Revenue)
            .ToList();

        var topMovers = productTotals
            .OrderByDescending(p => p.TotalRevenue)
            .ThenByDescending(p => p.TotalQuantitySold)
            .Take(MoversLimit)
            .Select(p => ToProductRow(p, productLookup))
            .ToList();

        var slowMovers = productTotals
            .OrderBy(p => p.TotalRevenue)
            .ThenBy(p => p.TotalQuantitySold)
            .Take(MoversLimit)
            .Select(p => ToProductRow(p, productLookup))
            .ToList();

        return new SalesAnalyticsDto
        {
            Days = days,
            FromDate = fromUtc,
            ToDate = todayStart,
            TotalRevenue = totalRevenue,
            TotalSales = totalSales,
            AvgSaleValue = totalSales > 0 ? Math.Round(totalRevenue / totalSales, 2) : 0m,
            DistinctProductsSold = productTotals.Count,
            DailySeries = series,
            Branches = branchSlices,
            CategoryMix = categoryMix,
            TopMovers = topMovers,
            SlowMovers = slowMovers
        };
    }

    /// <summary>
    /// Mirrors SaleAppService.GetListAsync scoping: ManageAll → unscoped (null);
    /// otherwise the caller's active managed branches. An explicit BranchId is
    /// intersected with that scope, never widened beyond it.
    /// </summary>
    private async Task<IReadOnlyCollection<Guid>?> ResolveBranchScopeAsync(Guid? branchId)
    {
        IReadOnlyCollection<Guid>? scope = null;

        if (!await AuthorizationService.IsGrantedAsync(OperationsPermissions.Sales.ManageAll))
        {
            var userId = CurrentUser.Id;
            if (userId == null)
            {
                scope = Array.Empty<Guid>();
            }
            else
            {
                var mine = await _branchRepository.GetListAsync(
                    b => b.IsActive && b.ManagerUserId == userId);
                scope = mine.ConvertAll(b => b.Id);
            }
        }

        if (!branchId.HasValue)
        {
            return scope;
        }

        if (scope == null || scope.Contains(branchId.Value))
        {
            return new[] { branchId.Value };
        }

        // Requested branch is outside the caller's scope → empty result, not an error.
        return Array.Empty<Guid>();
    }

    private static ProductSalesRowDto ToProductRow(
        ProductSalesAggregate aggregate,
        Dictionary<Guid, AppProduct> productLookup)
    {
        productLookup.TryGetValue(aggregate.ProductId, out var product);
        return new ProductSalesRowDto
        {
            ProductId = aggregate.ProductId,
            ProductName = product?.DisplayName ?? "(deleted product)",
            SKU = product?.SKU ?? string.Empty,
            QuantitySold = aggregate.TotalQuantitySold,
            Revenue = aggregate.TotalRevenue
        };
    }

    private static int NormalizeDays(int days)
        => days <= 7 ? 7 : days <= 30 ? 30 : 90;
}
