using System;
using System.Collections.Generic;

namespace patisserie_shop.Dashboard;

public class BranchDashboardDto
{
    /// <summary>
    /// True when the current user has no branch assigned via AppBranch.ManagerUserId.
    /// All other fields will be empty/zero in that case; the UI shows a friendly
    /// "no branch assigned" message instead of empty stats.
    /// </summary>
    public bool HasBranchAssigned { get; set; }

    /// <summary>True when the current user manages more than one branch.</summary>
    public bool HasMultipleBranches { get; set; }

    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;

    public int TotalProducts { get; set; }
    public int LowStockCount { get; set; }
    public int HealthyCount { get; set; }
    public int ExcessStockCount { get; set; }
    public int DeadStockCount { get; set; }

    public int PendingDecisionsCount { get; set; }
    public int IncomingTransfersCount { get; set; }

    public decimal TodaySalesTotal { get; set; }
    public int TodaySalesCount { get; set; }
    public decimal YesterdaySalesTotal { get; set; }
    public decimal WeekSalesTotal { get; set; }

    public List<StockItemDto> StockItems { get; set; } = new();
    public List<RecentDecisionDto> PendingDecisions { get; set; } = new();
    public List<RecentSaleDto> RecentSales { get; set; } = new();
    public List<DailySalesDto> SalesLast7Days { get; set; } = new();
    public List<IncomingTransferDto> IncomingTransfers { get; set; } = new();
}

public class StockItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int MinimumStock { get; set; }
    public int? MaximumStock { get; set; }
    public DateTime? LastSoldDate { get; set; }
    public int? DaysSinceLastSale { get; set; }
    /// <summary>"Low" | "Healthy" | "Excess" | "Dead"</summary>
    public string StockStatus { get; set; } = string.Empty;
}

public class RecentSaleDto
{
    public Guid SaleId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public int ItemCount { get; set; }
}

public class IncomingTransferDto
{
    public Guid TransferId { get; set; }
    public Guid FromBranchId { get; set; }
    public string FromBranchName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedDate { get; set; }
    public int ItemCount { get; set; }
}
