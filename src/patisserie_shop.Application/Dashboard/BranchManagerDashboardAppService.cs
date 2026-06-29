using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Intelligence.Entities;
using Inventory.BranchInventory;
using Inventory.Entities;
using Microsoft.AspNetCore.Authorization;
using Operations;
using Operations.Sales;
using Operations.StockTransfers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace patisserie_shop.Dashboard;

[Authorize]
public class BranchManagerDashboardAppService : patisserie_shopAppService, IBranchManagerDashboardAppService
{
    private const int StockItemsLimit = 15;
    private const int PendingDecisionsLimit = 8;
    private const int RecentSalesLimit = 10;
    private const int IncomingTransfersLimit = 10;
    private const int ExcessStockHighThreshold = 100;
    private const int DeadStockThresholdDays = 30;

    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IBranchInventoryRepository _inventoryRepository;
    private readonly IDecisionLogRepository _decisionLogRepository;
    private readonly ISaleRepository _saleRepository;
    private readonly IStockTransferRepository _stockTransferRepository;
    private readonly ICurrentUser _currentUser;

    public BranchManagerDashboardAppService(
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        IBranchInventoryRepository inventoryRepository,
        IDecisionLogRepository decisionLogRepository,
        ISaleRepository saleRepository,
        IStockTransferRepository stockTransferRepository,
        ICurrentUser currentUser)
    {
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _inventoryRepository = inventoryRepository;
        _decisionLogRepository = decisionLogRepository;
        _saleRepository = saleRepository;
        _stockTransferRepository = stockTransferRepository;
        _currentUser = currentUser;
    }

    public async Task<BranchDashboardDto> GetBranchDashboardAsync()
    {
        var userId = _currentUser.Id;
        if (userId == null)
        {
            return new BranchDashboardDto { HasBranchAssigned = false };
        }

        var myBranches = await _branchRepository.GetListAsync(b => b.ManagerUserId == userId);
        if (myBranches.Count == 0)
        {
            return new BranchDashboardDto { HasBranchAssigned = false };
        }

        var branch = myBranches.OrderBy(b => b.Name).First();
        var branchId = branch.Id;
        var hasMultiple = myBranches.Count > 1;

        var nowUtc = DateTime.UtcNow;
        var todayStart = nowUtc.Date;
        var tomorrowStart = todayStart.AddDays(1);
        var yesterdayStart = todayStart.AddDays(-1);
        var sevenDaysStart = todayStart.AddDays(-6);

        // Stock rows for this branch
        var stockRows = await _inventoryRepository.GetActiveStockRowsAsync(new[] { branchId });

        var stockItems = BuildStockItems(stockRows, nowUtc);

        // Counts derived from stock items
        var totalProducts = stockItems.Count;
        var lowStockCount = stockItems.Count(s => s.StockStatus == "Low");
        var excessCount = stockItems.Count(s => s.StockStatus == "Excess");
        var deadCount = stockItems.Count(s => s.StockStatus == "Dead");
        var healthyCount = totalProducts - lowStockCount - excessCount - deadCount;
        if (healthyCount < 0) healthyCount = 0;

        // Pending decisions for this branch + global. Split into two counts to dodge
        // an Npgsql translation quirk with `nullable == null || nullable == value`.
        var pendingForBranch = (int)await _decisionLogRepository.CountAsync(
            l => l.Status == DecisionLogStatuses.Pending && l.BranchId == branchId);
        var pendingGlobal = (int)await _decisionLogRepository.CountAsync(
            l => l.Status == DecisionLogStatuses.Pending && l.BranchId == null);
        var pendingDecisionsCount = pendingForBranch + pendingGlobal;

        var pendingDecisionRows = await _decisionLogRepository.GetFilteredListAsync(
            filter: null,
            decisionType: null,
            status: DecisionLogStatuses.Pending,
            branchId: null,
            productId: null,
            fromDate: null,
            toDate: null,
            scopedBranchIds: new[] { branchId },
            sorting: $"{nameof(AppDecisionLog.CreationTime)} desc",
            skipCount: 0,
            maxResultCount: PendingDecisionsLimit);

        // Sales aggregates for this branch
        var todaySales = await _saleRepository.GetDailySalesAsync(todayStart, tomorrowStart, new[] { branchId });
        var yesterdaySales = await _saleRepository.GetDailySalesAsync(yesterdayStart, todayStart, new[] { branchId });
        var weekDaily = await _saleRepository.GetDailySalesAsync(sevenDaysStart, tomorrowStart, new[] { branchId });

        var todayTotal = todaySales.Sum(d => d.TotalAmount);
        var todayCount = todaySales.Sum(d => d.SaleCount);
        var yesterdayTotal = yesterdaySales.Sum(d => d.TotalAmount);
        var weekTotal = weekDaily.Sum(d => d.TotalAmount);

        // Recent sales (most recent 10) for this branch
        var recentSaleRows = await _saleRepository.GetFilteredListAsync(
            branchIdScope: null,
            branchId: branchId,
            fromDate: null,
            toDate: null,
            filter: null,
            sorting: string.Empty,
            skipCount: 0,
            maxResultCount: RecentSalesLimit);

        var recentSales = recentSaleRows.Select(r => new RecentSaleDto
        {
            SaleId = r.Sale.Id,
            InvoiceNumber = r.Sale.InvoiceNumber,
            SaleDate = r.Sale.SaleDate,
            TotalAmount = r.Sale.TotalAmount,
            Currency = r.Sale.Currency,
            ItemCount = r.ItemCount
        }).ToList();

        // Incoming transfers for this branch
        var incomingRows = await _stockTransferRepository.GetActiveIncomingAsync(branchId, IncomingTransfersLimit);
        var sourceBranchIds = incomingRows
            .Where(r => r.Transfer.FromBranchId.HasValue)
            .Select(r => r.Transfer.FromBranchId!.Value)
            .Distinct()
            .ToList();
        var sourceBranches = sourceBranchIds.Count == 0
            ? new List<AppBranch>()
            : await _branchRepository.GetListAsync(b => sourceBranchIds.Contains(b.Id));
        var sourceBranchNames = sourceBranches.ToDictionary(b => b.Id, b => b.Name);

        var incomingTransfers = incomingRows.Select(r => new IncomingTransferDto
        {
            TransferId = r.Transfer.Id,
            FromBranchId = r.Transfer.FromBranchId,
            FromBranchName = r.Transfer.FromBranchId.HasValue &&
                             sourceBranchNames.TryGetValue(r.Transfer.FromBranchId.Value, out var n)
                ? n
                : "Waiting for admin",
            Status = r.Transfer.Status,
            RequestedDate = r.Transfer.RequestedDate,
            ItemCount = r.ItemCount
        }).ToList();

        // Build last-7-day sales series with empty days filled
        var dailyMap = weekDaily.ToDictionary(d => d.Date.Date, d => d);
        var salesSeries = new List<DailySalesDto>();
        for (var i = 6; i >= 0; i--)
        {
            var day = todayStart.AddDays(-i);
            if (dailyMap.TryGetValue(day, out var agg))
            {
                salesSeries.Add(new DailySalesDto { Date = day, TotalAmount = agg.TotalAmount, SaleCount = agg.SaleCount });
            }
            else
            {
                salesSeries.Add(new DailySalesDto { Date = day, TotalAmount = 0m, SaleCount = 0 });
            }
        }

        // Resolve names for pending decisions
        var allProducts = await _productRepository.GetListAsync();
        var productLookup = allProducts.ToDictionary(p => p.Id, p => p);
        var pendingDecisions = pendingDecisionRows.Select(r =>
        {
            var d = r.DecisionLog;
            productLookup.TryGetValue(d.ProductId, out var product);
            return new RecentDecisionDto
            {
                Id = d.Id,
                DecisionType = d.DecisionType,
                Reasoning = d.Reasoning,
                ProductName = product?.Name,
                BranchName = branch.Name,
                Status = d.Status,
                CreationTime = d.CreationTime,
                SuggestedAction = d.SuggestedAction
            };
        }).ToList();

        return new BranchDashboardDto
        {
            HasBranchAssigned = true,
            HasMultipleBranches = hasMultiple,
            BranchId = branchId,
            BranchName = branch.Name,
            TotalProducts = totalProducts,
            LowStockCount = lowStockCount,
            HealthyCount = healthyCount,
            ExcessStockCount = excessCount,
            DeadStockCount = deadCount,
            PendingDecisionsCount = pendingDecisionsCount,
            IncomingTransfersCount = incomingTransfers.Count,
            TodaySalesTotal = todayTotal,
            TodaySalesCount = todayCount,
            YesterdaySalesTotal = yesterdayTotal,
            WeekSalesTotal = weekTotal,
            StockItems = stockItems.Take(StockItemsLimit).ToList(),
            PendingDecisions = pendingDecisions,
            RecentSales = recentSales,
            SalesLast7Days = salesSeries,
            IncomingTransfers = incomingTransfers
        };
    }

    /// <summary>
    /// Classifies each stock row into Low / Healthy / Excess / Dead and sorts by
    /// urgency (Low → Dead → Excess → Healthy) so the dashboard list shows the
    /// items the branch manager needs to act on first.
    /// </summary>
    private static List<StockItemDto> BuildStockItems(List<InventoryStockRow> rows, DateTime nowUtc)
    {
        var items = rows.Select(r =>
        {
            var inv = r.Inventory;
            var product = r.Product;

            int? daysSinceSale = inv.LastSoldDate.HasValue
                ? Math.Max(0, (int)(nowUtc - inv.LastSoldDate.Value).TotalDays)
                : (int?)null;

            string status;
            if (inv.IsLowStock)
            {
                status = "Low";
            }
            else if (daysSinceSale != null && daysSinceSale >= DeadStockThresholdDays && inv.QuantityOnHand > 0)
            {
                status = "Dead";
            }
            else if (inv.MaximumStock.HasValue ? inv.QuantityOnHand > inv.MaximumStock.Value : inv.QuantityOnHand > ExcessStockHighThreshold)
            {
                status = "Excess";
            }
            else
            {
                status = "Healthy";
            }

            return new StockItemDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                SKU = product.SKU,
                Unit = product.Unit,
                QuantityOnHand = inv.QuantityOnHand,
                MinimumStock = inv.MinimumStock,
                MaximumStock = inv.MaximumStock,
                LastSoldDate = inv.LastSoldDate,
                DaysSinceLastSale = daysSinceSale,
                StockStatus = status
            };
        }).ToList();

        return items
            .OrderBy(s => s.StockStatus switch
            {
                "Low" => 0,
                "Dead" => 1,
                "Excess" => 2,
                _ => 3
            })
            .ThenBy(s => s.QuantityOnHand)
            .ThenBy(s => s.ProductName)
            .ToList();
    }
}
