using System;
using System.Collections.Generic;

namespace patisserie_shop.Dashboard;

public class AdminDashboardDto
{
    public int TotalProducts { get; set; }
    public int TotalBranches { get; set; }
    public int PendingDecisions { get; set; }
    public decimal TodaySalesTotal { get; set; }
    public decimal YesterdaySalesTotal { get; set; }
    public int TodaySalesCount { get; set; }
    public int LowStockProductCount { get; set; }
    public decimal WeekSalesTotal { get; set; }

    public List<BranchStockSummaryDto> BranchStockSummaries { get; set; } = new();
    public List<RecentDecisionDto> RecentDecisions { get; set; } = new();
    public List<DailySalesDto> SalesLast7Days { get; set; } = new();
    public PurchaseOrderPipelineDto PurchaseOrderPipeline { get; set; } = new();
    public List<TopProductDto> TopSellingProducts { get; set; } = new();
    public List<RecentMovementDto> RecentMovements { get; set; } = new();
    public List<BranchPerformanceDto> BranchPerformance { get; set; } = new();
}

public class BranchStockSummaryDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int LowStockCount { get; set; }
    public int ExcessStockCount { get; set; }
    public int HealthyCount { get; set; }
    public decimal TotalStockValue { get; set; }
}

public class RecentDecisionDto
{
    public Guid Id { get; set; }
    public string DecisionType { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string? BranchName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; }
    public string? SuggestedAction { get; set; }
}

public class DailySalesDto
{
    public DateTime Date { get; set; }
    public decimal TotalAmount { get; set; }
    public int SaleCount { get; set; }
}

public class PurchaseOrderPipelineDto
{
    public int DraftCount { get; set; }
    public int SubmittedCount { get; set; }
    public int ApprovedCount { get; set; }
    public int PartialReceivedCount { get; set; }
    public int ReceivedCount { get; set; }
}

public class TopProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public int TotalQuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class RecentMovementDto
{
    public string MovementType { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int QuantityAfter { get; set; }
    public DateTime CreationTime { get; set; }
}

public class BranchPerformanceDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public decimal SalesToday { get; set; }
    public decimal SalesThisWeek { get; set; }
    public int PendingAlerts { get; set; }
    public int LowStockItems { get; set; }
    public int HealthyItems { get; set; }
    public int ExcessItems { get; set; }
    public decimal TotalStockValue { get; set; }
}
