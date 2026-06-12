using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Permissions;
using Inventory.StockMovements;
using Microsoft.AspNetCore.Authorization;
using Operations.Sales;
using Volo.Abp.Domain.Repositories;

namespace patisserie_shop.Analytics;

/// <summary>
/// Composes Inventory WriteOff-movement aggregates (already valued at product
/// CostPrice by <see cref="IStockMovementRepository.GetWriteOffAggregatesAsync"/>)
/// with branch/product names and the Operations sales totals in the host app —
/// same cross-module, in-memory composition pattern as
/// <see cref="SalesAnalyticsAppService"/>. All heavy grouping happens in the
/// repositories; this service only buckets day-rows into weeks and maps
/// read-models onto DTOs.
/// </summary>
[Authorize(InventoryPermissions.StockMovements.Default)]
public class WasteAnalyticsAppService : patisserie_shopAppService, IWasteAnalyticsAppService
{
    private const int TopProductsLimit = 10;

    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;

    public WasteAnalyticsAppService(
        IStockMovementRepository stockMovementRepository,
        ISaleRepository saleRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository)
    {
        _stockMovementRepository = stockMovementRepository;
        _saleRepository = saleRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
    }

    public async Task<WasteAnalyticsDto> GetAsync(GetWasteAnalyticsInput input)
    {
        var days = NormalizeDays(input.Days);

        var todayStart = DateTime.UtcNow.Date;
        var fromUtc = todayStart.AddDays(-(days - 1));
        var toUtc = todayStart.AddDays(1);

        var branchScope = await ResolveBranchScopeAsync(input.BranchId);

        var wasteRows = await _stockMovementRepository.GetWriteOffAggregatesAsync(fromUtc, toUtc, branchScope);

        // Sales denominator over the SAME window and branch scope, so the
        // waste-to-sales ratio compares like with like.
        var salesByBranch = await _saleRepository.GetSalesByBranchAsync(fromUtc, toUtc, branchScope);
        var totalSalesRevenue = salesByBranch.Sum(s => s.TotalAmount);

        // Reference data for display names (small sets — same in-memory
        // dictionary composition the dashboards use).
        var branchNames = (await _branchRepository.GetListAsync())
            .ToDictionary(b => b.Id, b => b.Name);
        var productLookup = (await _productRepository.GetListAsync())
            .ToDictionary(p => p.Id, p => p);

        var totalWasteCost = wasteRows.Sum(r => r.Cost);
        var totalUnits = wasteRows.Sum(r => r.Units);

        // Weekly series: 7-day buckets anchored at the window start, zero-filled so
        // the chart axis is continuous (the last bucket may be a partial week).
        var bucketCount = (days + 6) / 7;
        var series = new List<WeeklyWastePointDto>(bucketCount);
        for (var i = 0; i < bucketCount; i++)
        {
            series.Add(new WeeklyWastePointDto { WeekStart = fromUtc.AddDays(i * 7) });
        }
        foreach (var row in wasteRows)
        {
            var index = Math.Clamp((int)(row.Date.Date - fromUtc).TotalDays / 7, 0, bucketCount - 1);
            series[index].Cost += row.Cost;
            series[index].Units += row.Units;
        }

        var branchSlices = wasteRows
            .GroupBy(r => r.BranchId)
            .Select(g => new BranchWasteSliceDto
            {
                BranchId = g.Key,
                BranchName = branchNames.GetValueOrDefault(g.Key, "(deleted branch)"),
                Cost = g.Sum(r => r.Cost),
                Units = g.Sum(r => r.Units)
            })
            .OrderByDescending(b => b.Cost)
            .ToList();

        var productTotals = wasteRows
            .GroupBy(r => r.ProductId)
            .Select(g =>
            {
                productLookup.TryGetValue(g.Key, out var product);
                return new ProductWasteRowDto
                {
                    ProductId = g.Key,
                    ProductName = product?.Name ?? "(deleted product)",
                    SKU = product?.SKU ?? string.Empty,
                    Units = g.Sum(r => r.Units),
                    Cost = g.Sum(r => r.Cost)
                };
            })
            .OrderByDescending(p => p.Cost)
            .ThenByDescending(p => p.Units)
            .ToList();

        return new WasteAnalyticsDto
        {
            Days = days,
            FromDate = fromUtc,
            ToDate = todayStart,
            TotalWasteCost = totalWasteCost,
            TotalUnitsWrittenOff = totalUnits,
            WasteToSalesPercent = totalSalesRevenue > 0
                ? Math.Round(totalWasteCost / totalSalesRevenue * 100, 2)
                : 0m,
            TopWastedProductName = productTotals.Count > 0 ? productTotals[0].ProductName : null,
            WeeklySeries = series,
            Branches = branchSlices,
            TopProducts = productTotals.Take(TopProductsLimit).ToList()
        };
    }

    /// <summary>
    /// Mirrors StockMovementAppService scoping: StockMovements.ViewAll → unscoped
    /// (null); otherwise the caller's active managed branches. An explicit BranchId
    /// is intersected with that scope, never widened beyond it — same contract as
    /// <see cref="SalesAnalyticsAppService"/>.
    /// </summary>
    private async Task<IReadOnlyCollection<Guid>?> ResolveBranchScopeAsync(Guid? branchId)
    {
        IReadOnlyCollection<Guid>? scope = null;

        if (!await AuthorizationService.IsGrantedAsync(InventoryPermissions.StockMovements.ViewAll))
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

    private static int NormalizeDays(int days)
        => days <= 30 ? 30 : 90;
}
