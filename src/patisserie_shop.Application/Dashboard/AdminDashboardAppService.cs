using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Intelligence.Entities;
using Intelligence.Permissions;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Permissions;
using Inventory.StockMovements;
using Microsoft.AspNetCore.Authorization;
using Operations;
using Operations.Entities;
using Operations.Sales;
using Volo.Abp.Domain.Repositories;

namespace patisserie_shop.Dashboard;

[Authorize(InventoryPermissions.BranchInventory.ManageAll)]
public class AdminDashboardAppService : patisserie_shopAppService, IAdminDashboardAppService
{
    private const int RecentDecisionsLimit = 10;
    private const int RecentMovementsLimit = 10;
    private const int TopProductsLimit = 5;
    private const int ExcessStockHighThreshold = 100;

    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IBranchInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _movementRepository;
    private readonly IDecisionLogRepository _decisionLogRepository;
    private readonly IRepository<AppPurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly ISaleRepository _saleRepository;

    public AdminDashboardAppService(
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IBranchInventoryRepository inventoryRepository,
        IStockMovementRepository movementRepository,
        IDecisionLogRepository decisionLogRepository,
        IRepository<AppPurchaseOrder, Guid> purchaseOrderRepository,
        ISaleRepository saleRepository)
    {
        _productRepository = productRepository;
        _branchRepository = branchRepository;
        _inventoryRepository = inventoryRepository;
        _movementRepository = movementRepository;
        _decisionLogRepository = decisionLogRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _saleRepository = saleRepository;
    }

    public async Task<AdminDashboardDto> GetAdminDashboardAsync()
    {
        var nowUtc = DateTime.UtcNow;
        var todayStart = nowUtc.Date;
        var tomorrowStart = todayStart.AddDays(1);
        var yesterdayStart = todayStart.AddDays(-1);
        var sevenDaysStart = todayStart.AddDays(-6);
        var weekStart = todayStart.AddDays(-6);

        // Counts
        var totalProducts = (int)await _productRepository.CountAsync();
        var branches = await _branchRepository.GetListAsync();
        var totalBranches = branches.Count;

        // Stock rows across all branches (admin scope)
        var stockRows = await _inventoryRepository.GetActiveStockRowsAsync(branchIdScope: null);

        var lowStockProductCount = stockRows
            .Where(r => r.Inventory.IsLowStock)
            .Select(r => r.Product.Id)
            .Distinct()
            .Count();

        // Decisions
        var pendingDecisions = (int)await _decisionLogRepository.CountAsync(
            l => l.Status == DecisionLogStatuses.Pending);

        var recentDecisionRows = await _decisionLogRepository.GetFilteredListAsync(
            filter: null,
            decisionType: null,
            status: DecisionLogStatuses.Pending,
            branchId: null,
            productId: null,
            fromDate: null,
            toDate: null,
            scopedBranchIds: null,
            sorting: $"{nameof(AppDecisionLog.CreationTime)} desc",
            skipCount: 0,
            maxResultCount: RecentDecisionsLimit);

        // Sales aggregates
        var todaySales = await _saleRepository.GetDailySalesAsync(todayStart, tomorrowStart, null);
        var yesterdaySales = await _saleRepository.GetDailySalesAsync(yesterdayStart, todayStart, null);
        var weekDaily = await _saleRepository.GetDailySalesAsync(sevenDaysStart, tomorrowStart, null);
        var topProducts = await _saleRepository.GetTopProductsAsync(sevenDaysStart, tomorrowStart, null, TopProductsLimit);
        var salesByBranchToday = await _saleRepository.GetSalesByBranchAsync(todayStart, tomorrowStart, null);
        var salesByBranchWeek = await _saleRepository.GetSalesByBranchAsync(weekStart, tomorrowStart, null);

        var todayTotal = todaySales.Sum(d => d.TotalAmount);
        var todayCount = todaySales.Sum(d => d.SaleCount);
        var yesterdayTotal = yesterdaySales.Sum(d => d.TotalAmount);
        var weekTotal = weekDaily.Sum(d => d.TotalAmount);

        // PO Pipeline
        var pipeline = new PurchaseOrderPipelineDto
        {
            DraftCount = (int)await _purchaseOrderRepository.CountAsync(p => p.Status == PurchaseOrderStatuses.Draft),
            SubmittedCount = (int)await _purchaseOrderRepository.CountAsync(p => p.Status == PurchaseOrderStatuses.Submitted),
            ApprovedCount = (int)await _purchaseOrderRepository.CountAsync(p => p.Status == PurchaseOrderStatuses.Approved),
            PartialReceivedCount = (int)await _purchaseOrderRepository.CountAsync(p => p.Status == PurchaseOrderStatuses.PartialReceived),
            ReceivedCount = (int)await _purchaseOrderRepository.CountAsync(p => p.Status == PurchaseOrderStatuses.Received),
        };

        // Recent movements
        var since = nowUtc.AddDays(-30);
        var recentMovementRows = await _movementRepository.GetRecentWithContextAsync(branchIdScope: null, since);

        // Product lookup for top products + recent decisions
        var productLookup = (await _productRepository.GetListAsync())
            .ToDictionary(p => p.Id, p => p);
        var branchLookup = branches.ToDictionary(b => b.Id, b => b);

        // Pending alerts per branch
        var pendingDecisionsByBranch = new Dictionary<Guid, int>();
        var pendingDecisionsList = await _decisionLogRepository.GetListAsync(
            l => l.Status == DecisionLogStatuses.Pending);
        foreach (var d in pendingDecisionsList)
        {
            if (!d.BranchId.HasValue) continue;
            pendingDecisionsByBranch.TryGetValue(d.BranchId.Value, out var n);
            pendingDecisionsByBranch[d.BranchId.Value] = n + 1;
        }

        // Branch stock summaries
        var rowsByBranch = stockRows.GroupBy(r => r.Inventory.BranchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var branchSummaries = branches.Select(b =>
        {
            var rows = rowsByBranch.TryGetValue(b.Id, out var list) ? list : new List<InventoryStockRow>();
            var low = rows.Count(r => r.Inventory.IsLowStock);
            var excess = rows.Count(r => !r.Inventory.IsLowStock
                && (r.Inventory.MaximumStock.HasValue
                    ? r.Inventory.QuantityOnHand > r.Inventory.MaximumStock.Value
                    : r.Inventory.QuantityOnHand > ExcessStockHighThreshold));
            var healthy = rows.Count - low - excess;
            if (healthy < 0) healthy = 0;
            var value = rows.Sum(r => r.Inventory.QuantityOnHand * r.Product.CostPrice);
            return new BranchStockSummaryDto
            {
                BranchId = b.Id,
                BranchName = b.DisplayName,
                TotalProducts = rows.Count,
                LowStockCount = low,
                ExcessStockCount = excess,
                HealthyCount = healthy,
                TotalStockValue = value
            };
        }).ToList();

        var todayByBranch = salesByBranchToday.ToDictionary(s => s.BranchId, s => s.TotalAmount);
        var weekByBranch = salesByBranchWeek.ToDictionary(s => s.BranchId, s => s.TotalAmount);

        var branchPerformance = branches.Select(b =>
        {
            var summary = branchSummaries.First(s => s.BranchId == b.Id);
            pendingDecisionsByBranch.TryGetValue(b.Id, out var pending);
            todayByBranch.TryGetValue(b.Id, out var todayAmt);
            weekByBranch.TryGetValue(b.Id, out var weekAmt);
            return new BranchPerformanceDto
            {
                BranchId = b.Id,
                BranchName = b.DisplayName,
                SalesToday = todayAmt,
                SalesThisWeek = weekAmt,
                PendingAlerts = pending,
                LowStockItems = summary.LowStockCount,
                HealthyItems = summary.HealthyCount,
                ExcessItems = summary.ExcessStockCount,
                TotalStockValue = summary.TotalStockValue
            };
        })
        .OrderByDescending(b => b.PendingAlerts)
        .ThenByDescending(b => b.LowStockItems)
        .ToList();

        // Build last-7-day series (fill empty days with zero)
        var dailyMap = weekDaily.ToDictionary(d => d.Date.Date, d => d);
        var salesSeries = new List<DailySalesDto>();
        for (var i = 6; i >= 0; i--)
        {
            var day = todayStart.AddDays(-i);
            if (dailyMap.TryGetValue(day, out var agg))
            {
                salesSeries.Add(new DailySalesDto
                {
                    Date = day,
                    TotalAmount = agg.TotalAmount,
                    SaleCount = agg.SaleCount
                });
            }
            else
            {
                salesSeries.Add(new DailySalesDto { Date = day, TotalAmount = 0m, SaleCount = 0 });
            }
        }

        var topProductDtos = topProducts.Select(p =>
        {
            productLookup.TryGetValue(p.ProductId, out var product);
            return new TopProductDto
            {
                ProductId = p.ProductId,
                ProductName = product?.DisplayName ?? "(deleted product)",
                SKU = product?.SKU ?? string.Empty,
                TotalQuantitySold = p.TotalQuantitySold,
                TotalRevenue = p.TotalRevenue
            };
        }).ToList();

        var recentDecisions = recentDecisionRows.Select(r =>
        {
            var d = r.DecisionLog;
            productLookup.TryGetValue(d.ProductId, out var product);
            string? branchName = null;
            if (d.BranchId.HasValue && branchLookup.TryGetValue(d.BranchId.Value, out var br))
                branchName = br.DisplayName;
            return new RecentDecisionDto
            {
                Id = d.Id,
                DecisionType = d.DecisionType,
                Reasoning = d.Reasoning,
                ProductName = product?.DisplayName,
                BranchName = branchName,
                Status = d.Status,
                CreationTime = d.CreationTime,
                SuggestedAction = d.SuggestedAction
            };
        }).ToList();

        var recentMovements = recentMovementRows.Take(RecentMovementsLimit).Select(m => new RecentMovementDto
        {
            MovementType = m.Movement.MovementType,
            ProductName = m.Product.DisplayName,
            BranchName = m.Branch.DisplayName,
            Quantity = m.Movement.Quantity,
            QuantityAfter = m.Movement.QuantityAfter,
            CreationTime = m.Movement.CreationTime
        }).ToList();

        return new AdminDashboardDto
        {
            TotalProducts = totalProducts,
            TotalBranches = totalBranches,
            PendingDecisions = pendingDecisions,
            TodaySalesTotal = todayTotal,
            YesterdaySalesTotal = yesterdayTotal,
            TodaySalesCount = todayCount,
            LowStockProductCount = lowStockProductCount,
            WeekSalesTotal = weekTotal,
            BranchStockSummaries = branchSummaries,
            RecentDecisions = recentDecisions,
            SalesLast7Days = salesSeries,
            PurchaseOrderPipeline = pipeline,
            TopSellingProducts = topProductDtos,
            RecentMovements = recentMovements,
            BranchPerformance = branchPerformance
        };
    }
}
