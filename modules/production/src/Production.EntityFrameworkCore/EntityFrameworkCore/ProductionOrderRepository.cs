using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Production.Entities;
using Production.Orders;
using Production.Reports;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Production.EntityFrameworkCore;

public class ProductionOrderRepository
    : EfCoreRepository<ProductionDbContext, AppProductionOrder, Guid>,
      IProductionOrderRepository
{
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;

    public ProductionOrderRepository(
        IDbContextProvider<ProductionDbContext> dbContextProvider,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository)
        : base(dbContextProvider)
    {
        _branchRepository = branchRepository;
        _productRepository = productRepository;
    }

    public async Task<AppProductionOrder> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await WithDetailsAsync(o => o.Ingredients, o => o.Allocations);
        return await query.FirstAsync(o => o.Id == id, GetCancellationToken(cancellationToken));
    }

    public async Task<AppProductionOrder?> FindByPlanLineAsync(
        Guid productionPlanLineId,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query.FirstOrDefaultAsync(
            o => o.ProductionPlanLineId == productionPlanLineId,
            GetCancellationToken(cancellationToken));
    }

    public async Task<long> CountFilteredAsync(
        string? filter,
        string? status,
        Guid? kitchenBranchId,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, status, kitchenBranchId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<ProductionOrderListItem>> GetFilteredListAsync(
        string? filter,
        string? status,
        Guid? kitchenBranchId,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);
        var query = await BuildFilteredQueryAsync(filter, status, kitchenBranchId);

        var headers = await query
            .Select(o => new HeaderProjection
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                KitchenBranchId = o.KitchenBranchId,
                FinishedProductId = o.FinishedProductId,
                Status = o.Status,
                Priority = o.Priority,
                PlannedOutputQuantity = o.PlannedOutputQuantity,
                AcceptedQuantity = o.AcceptedQuantity,
                ActualStartTime = o.ActualStartTime,
                CompletedAt = o.CompletedAt,
                TotalProductionCost = o.TotalProductionCost
            })
            .ToListAsync(ct);

        if (headers.Count == 0)
        {
            return new List<ProductionOrderListItem>();
        }

        var branchIds = headers.Select(h => h.KitchenBranchId).Distinct().ToList();
        var productIds = headers.Select(h => h.FinishedProductId).Distinct().ToList();

        var branches = await _branchRepository.GetListAsync(b => branchIds.Contains(b.Id), cancellationToken: ct);
        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id), cancellationToken: ct);

        var branchById = branches.ToDictionary(b => b.Id);
        var productById = products.ToDictionary(p => p.Id);

        var rows = headers.Select(h =>
        {
            branchById.TryGetValue(h.KitchenBranchId, out var branch);
            productById.TryGetValue(h.FinishedProductId, out var product);

            return new ProductionOrderListItem
            {
                Id = h.Id,
                OrderNumber = h.OrderNumber,
                KitchenBranchId = h.KitchenBranchId,
                KitchenBranchName = branch?.Name ?? h.KitchenBranchId.ToString(),
                FinishedProductId = h.FinishedProductId,
                FinishedProductName = product?.Name ?? h.FinishedProductId.ToString(),
                FinishedProductSku = product?.SKU ?? string.Empty,
                Unit = product?.Unit ?? string.Empty,
                Status = h.Status,
                Priority = h.Priority,
                PlannedOutputQuantity = h.PlannedOutputQuantity,
                AcceptedQuantity = h.AcceptedQuantity,
                ActualStartTime = h.ActualStartTime,
                CompletedAt = h.CompletedAt,
                TotalProductionCost = h.TotalProductionCost
            };
        });

        return ApplySorting(rows, sorting)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToList();
    }

    public async Task<ProductionDashboardReadModel> GetDashboardAsync(
        Guid? kitchenBranchId,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);
        var dbContext = await GetDbContextAsync();
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var weekStart = today.AddDays(-6);

        var ordersQuery = dbContext.ProductionOrders.AsNoTracking().AsQueryable();
        var wasteQuery = dbContext.ProductionWastes.AsNoTracking().AsQueryable();

        if (kitchenBranchId.HasValue)
        {
            ordersQuery = ordersQuery.Where(o => o.KitchenBranchId == kitchenBranchId.Value);
            wasteQuery = wasteQuery.Where(w => w.KitchenBranchId == kitchenBranchId.Value);
        }

        var activeOrders = await ordersQuery
            .Where(o => o.Status == ProductionOrderStatuses.WaitingForIngredients
                     || o.Status == ProductionOrderStatuses.ReadyToCook
                     || o.Status == ProductionOrderStatuses.InProduction)
            .ToListAsync(ct);

        var completedWeek = await ordersQuery
            .Where(o => o.CompletedAt != null && o.CompletedAt >= weekStart && o.CompletedAt < tomorrow)
            .ToListAsync(ct);

        var wasteToday = await wasteQuery
            .Where(w => w.RecordedAt >= today && w.RecordedAt < tomorrow)
            .ToListAsync(ct);

        var requestStatuses = ApprovedDemandStatuses();
        var openRequests = await dbContext.BranchProductionRequests
            .AsNoTracking()
            .Include(r => r.Items)
            .Where(r => requestStatuses.Contains(r.Status))
            .ToListAsync(ct);

        var pendingRequests = await dbContext.BranchProductionRequests
            .AsNoTracking()
            .CountAsync(r => r.Status == BranchProductionRequestStatuses.Submitted, ct);

        var unfulfilledDueToday = openRequests.Count(r =>
            r.NeededByDate.Date <= today
            && r.Items.Any(i => i.ApprovedQuantity > i.FulfilledQuantity));

        var approvedQuantity = openRequests.Sum(r => r.Items.Sum(i => i.ApprovedQuantity));
        var fulfilledQuantity = openRequests.Sum(r => r.Items.Sum(i => i.FulfilledQuantity));

        var completedToday = completedWeek
            .Where(o => o.CompletedAt >= today && o.CompletedAt < tomorrow)
            .ToList();
        var acceptedToday = completedToday.Sum(o => o.AcceptedQuantity);
        var rejectedToday = completedToday.Sum(o => o.RejectedQuantity);

        var dashboard = new ProductionDashboardReadModel
        {
            PendingRequests = pendingRequests,
            UnfulfilledDueToday = unfulfilledDueToday,
            WaitingForIngredients = activeOrders.Count(o => o.Status == ProductionOrderStatuses.WaitingForIngredients),
            ReadyToCook = activeOrders.Count(o => o.Status == ProductionOrderStatuses.ReadyToCook),
            InProduction = activeOrders.Count(o => o.Status == ProductionOrderStatuses.InProduction),
            CompletedToday = completedToday.Count,
            AcceptedToday = acceptedToday,
            RejectedToday = rejectedToday,
            WasteCostToday = wasteToday.Sum(w => w.TotalCost),
            YieldPercentToday = Percent(acceptedToday, acceptedToday + rejectedToday),
            FulfillmentPercent = Percent(fulfilledQuantity, approvedQuantity)
        };

        for (var date = weekStart; date <= today; date = date.AddDays(1))
        {
            var day = date;
            var rows = completedWeek.Where(o => o.CompletedAt >= day && o.CompletedAt < day.AddDays(1)).ToList();
            dashboard.OutputLast7Days.Add(new ProductionDailyOutputPoint
            {
                Date = day,
                AcceptedQuantity = rows.Sum(o => o.AcceptedQuantity),
                RejectedQuantity = rows.Sum(o => o.RejectedQuantity),
                TotalCost = rows.Sum(o => o.TotalProductionCost)
            });
        }

        dashboard.ProductFocus = await BuildProductFocusAsync(completedWeek, ct);
        dashboard.ActionItems = BuildDashboardActions(dashboard);
        return dashboard;
    }

    public async Task<ProductionAnalyticsReadModel> GetAnalyticsAsync(
        int days,
        Guid? kitchenBranchId,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);
        var dbContext = await GetDbContextAsync();
        var windowDays = Math.Max(1, Math.Min(days, 365));
        var today = DateTime.UtcNow.Date;
        var from = today.AddDays(-windowDays + 1);
        var tomorrow = today.AddDays(1);

        var ordersQuery = dbContext.ProductionOrders.AsNoTracking().AsQueryable();
        var wasteQuery = dbContext.ProductionWastes.AsNoTracking().AsQueryable();

        if (kitchenBranchId.HasValue)
        {
            ordersQuery = ordersQuery.Where(o => o.KitchenBranchId == kitchenBranchId.Value);
            wasteQuery = wasteQuery.Where(w => w.KitchenBranchId == kitchenBranchId.Value);
        }

        var completedOrders = await ordersQuery
            .Where(o => o.CompletedAt != null && o.CompletedAt >= from && o.CompletedAt < tomorrow)
            .ToListAsync(ct);

        var requestStatuses = ApprovedDemandStatuses();
        var requests = await dbContext.BranchProductionRequests
            .AsNoTracking()
            .Include(r => r.Items)
            .Where(r => r.NeededByDate >= from
                     && r.NeededByDate < tomorrow
                     && requestStatuses.Contains(r.Status))
            .ToListAsync(ct);

        var wastes = await wasteQuery
            .Where(w => w.RecordedAt >= from && w.RecordedAt < tomorrow)
            .ToListAsync(ct);

        var plannedQuantity = completedOrders.Sum(o => o.PlannedOutputQuantity);
        var acceptedQuantity = completedOrders.Sum(o => o.AcceptedQuantity);
        var rejectedQuantity = completedOrders.Sum(o => o.RejectedQuantity);
        var approvedRequestQuantity = requests.Sum(r => r.Items.Sum(i => i.ApprovedQuantity));
        var fulfilledRequestQuantity = requests.Sum(r => r.Items.Sum(i => i.FulfilledQuantity));
        var plannedCost = completedOrders.Sum(o => o.PlannedIngredientCost + o.LaborCost + o.OverheadCost);
        var actualCost = completedOrders.Sum(o => o.TotalProductionCost);
        var variance = actualCost - plannedCost;

        var analytics = new ProductionAnalyticsReadModel
        {
            Days = windowDays,
            PlannedQuantity = plannedQuantity,
            AcceptedQuantity = acceptedQuantity,
            RejectedQuantity = rejectedQuantity,
            ApprovedRequestQuantity = approvedRequestQuantity,
            FulfilledRequestQuantity = fulfilledRequestQuantity,
            YieldPercent = Percent(acceptedQuantity, acceptedQuantity + rejectedQuantity),
            WastePercent = Percent(rejectedQuantity, acceptedQuantity + rejectedQuantity),
            FulfillmentPercent = Percent(fulfilledRequestQuantity, approvedRequestQuantity),
            PlannedCost = plannedCost,
            ActualCost = actualCost,
            CostVariance = variance,
            CostVariancePercent = plannedCost > 0 ? Math.Round(variance / plannedCost * 100m, 2) : 0m,
            AverageUnitCost = acceptedQuantity > 0 ? Math.Round(actualCost / acceptedQuantity, 4) : 0m
        };

        for (var date = from; date <= today; date = date.AddDays(1))
        {
            var day = date;
            var rows = completedOrders.Where(o => o.CompletedAt >= day && o.CompletedAt < day.AddDays(1)).ToList();
            analytics.DailyOutput.Add(new ProductionDailyOutputPoint
            {
                Date = day,
                AcceptedQuantity = rows.Sum(o => o.AcceptedQuantity),
                RejectedQuantity = rows.Sum(o => o.RejectedQuantity),
                TotalCost = rows.Sum(o => o.TotalProductionCost)
            });
        }

        analytics.ProductPerformance = await BuildProductFocusAsync(completedOrders, ct);
        analytics.WasteReasons = wastes
            .GroupBy(w => w.Reason)
            .Select(g => new ProductionWasteReasonAnalyticsRow
            {
                Reason = g.Key,
                Quantity = g.Sum(w => w.Quantity),
                Cost = g.Sum(w => w.TotalCost)
            })
            .OrderByDescending(r => r.Quantity)
            .ToList();

        return analytics;
    }

    private async Task<IQueryable<AppProductionOrder>> BuildFilteredQueryAsync(
        string? filter,
        string? status,
        Guid? kitchenBranchId)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(o => o.OrderNumber.ToLower().Contains(f));
        }

        if (!string.IsNullOrWhiteSpace(status) && ProductionOrderStatuses.All.Contains(status))
        {
            query = query.Where(o => o.Status == status);
        }

        if (kitchenBranchId.HasValue)
        {
            query = query.Where(o => o.KitchenBranchId == kitchenBranchId.Value);
        }

        return query;
    }

    private static IEnumerable<ProductionOrderListItem> ApplySorting(
        IEnumerable<ProductionOrderListItem> rows,
        string sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
        {
            return rows
                .OrderBy(r => StatusRank(r.Status))
                .ThenBy(r => r.FinishedProductName, StringComparer.OrdinalIgnoreCase);
        }

        var parts = sorting.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var field = parts[0];
        var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        Func<ProductionOrderListItem, object?> keySelector = field.ToLowerInvariant() switch
        {
            "ordernumber" => r => r.OrderNumber,
            "finishedproductname" => r => r.FinishedProductName,
            "kitchenbranchname" => r => r.KitchenBranchName,
            "status" => r => r.Status,
            "plannedoutputquantity" => r => r.PlannedOutputQuantity,
            "totalproductioncost" => r => r.TotalProductionCost,
            _ => r => r.OrderNumber
        };

        return descending ? rows.OrderByDescending(keySelector) : rows.OrderBy(keySelector);
    }

    private static int StatusRank(string status) => status switch
    {
        ProductionOrderStatuses.ReadyToCook => 0,
        ProductionOrderStatuses.WaitingForIngredients => 1,
        ProductionOrderStatuses.InProduction => 2,
        ProductionOrderStatuses.Draft => 3,
        ProductionOrderStatuses.Completed => 4,
        ProductionOrderStatuses.Cancelled => 5,
        _ => 9
    };

    private async Task<List<ProductionProductFocusRow>> BuildProductFocusAsync(
        List<AppProductionOrder> orders,
        CancellationToken cancellationToken)
    {
        if (orders.Count == 0)
        {
            return new List<ProductionProductFocusRow>();
        }

        var productIds = orders.Select(o => o.FinishedProductId).Distinct().ToList();
        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id), cancellationToken: cancellationToken);
        var productById = products.ToDictionary(p => p.Id);

        return orders
            .GroupBy(o => o.FinishedProductId)
            .Select(g =>
            {
                productById.TryGetValue(g.Key, out var product);
                var accepted = g.Sum(o => o.AcceptedQuantity);
                var rejected = g.Sum(o => o.RejectedQuantity);
                var totalCost = g.Sum(o => o.TotalProductionCost);

                return new ProductionProductFocusRow
                {
                    ProductId = g.Key,
                    ProductName = product?.Name ?? g.Key.ToString(),
                    ProductSku = product?.SKU ?? string.Empty,
                    AcceptedQuantity = accepted,
                    RejectedQuantity = rejected,
                    YieldPercent = Percent(accepted, accepted + rejected),
                    UnitCost = accepted > 0 ? Math.Round(totalCost / accepted, 4) : 0m
                };
            })
            .OrderByDescending(r => r.AcceptedQuantity + r.RejectedQuantity)
            .Take(8)
            .ToList();
    }

    private static List<ProductionActionItemReadModel> BuildDashboardActions(ProductionDashboardReadModel dashboard)
    {
        var actions = new List<ProductionActionItemReadModel>();

        if (dashboard.UnfulfilledDueToday > 0)
        {
            actions.Add(new ProductionActionItemReadModel
            {
                Type = "UnfulfilledBranchRequest",
                Severity = "Danger",
                Title = "طلبات فروع مستحقة اليوم لم تُغلق بعد",
                Detail = "راجع لوحة الصرف وحوّل الكميات الجاهزة للفروع قبل نهاية اليوم.",
                Url = "/production/dispatch",
                Count = dashboard.UnfulfilledDueToday
            });
        }

        if (dashboard.WaitingForIngredients > 0)
        {
            actions.Add(new ProductionActionItemReadModel
            {
                Type = "IngredientShortage",
                Severity = "Warning",
                Title = "أوامر طبخ تنتظر خامات",
                Detail = "افتح شاشة الطبخ وأنشئ طلبات شراء خامات للأوامر الناقصة.",
                Url = "/production/cook",
                Count = dashboard.WaitingForIngredients
            });
        }

        if (dashboard.ReadyToCook > 0)
        {
            actions.Add(new ProductionActionItemReadModel
            {
                Type = "ReadyToCook",
                Severity = "Success",
                Title = "أوامر جاهزة للطبخ",
                Detail = "الخامات متوفرة. ابدأ بالأولويات العاجلة ثم العادية.",
                Url = "/production/cook",
                Count = dashboard.ReadyToCook
            });
        }

        if (dashboard.InProduction > 0)
        {
            actions.Add(new ProductionActionItemReadModel
            {
                Type = "LateProductionRisk",
                Severity = "Info",
                Title = "أوامر تحت الطبخ",
                Detail = "تابع أوقات التشغيل وسجّل الناتج المقبول والمرفوض عند الانتهاء.",
                Url = "/production/cook",
                Count = dashboard.InProduction
            });
        }

        if (dashboard.RejectedToday > 0)
        {
            actions.Add(new ProductionActionItemReadModel
            {
                Type = "HighKitchenWaste",
                Severity = dashboard.YieldPercentToday < 90m ? "Warning" : "Info",
                Title = "هدر مطبخ مسجّل اليوم",
                Detail = "افتح سجل الهدر لمعرفة السبب والمنتجات الأكثر تأثرًا.",
                Url = "/production/waste",
                Count = dashboard.RejectedToday
            });
        }

        if (dashboard.PendingRequests > 0)
        {
            actions.Add(new ProductionActionItemReadModel
            {
                Type = "PendingRequests",
                Severity = "Info",
                Title = "طلبات فروع بانتظار القرار",
                Detail = "راجع الكميات المطلوبة واعتمد ما يمكن إنتاجه.",
                Url = "/production/branch-requests",
                Count = dashboard.PendingRequests
            });
        }

        return actions;
    }

    private static string[] ApprovedDemandStatuses() =>
    [
        BranchProductionRequestStatuses.Approved,
        BranchProductionRequestStatuses.PartiallyPlanned,
        BranchProductionRequestStatuses.Planned,
        BranchProductionRequestStatuses.PartiallyFulfilled
    ];

    private static decimal Percent(decimal numerator, decimal denominator)
        => denominator <= 0 ? 0m : Math.Round(numerator / denominator * 100m, 2);

    private sealed class HeaderProjection
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = null!;
        public Guid KitchenBranchId { get; set; }
        public Guid FinishedProductId { get; set; }
        public string Status { get; set; } = null!;
        public string Priority { get; set; } = null!;
        public int PlannedOutputQuantity { get; set; }
        public int AcceptedQuantity { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? CompletedAt { get; set; }
        public decimal TotalProductionCost { get; set; }
    }
}
