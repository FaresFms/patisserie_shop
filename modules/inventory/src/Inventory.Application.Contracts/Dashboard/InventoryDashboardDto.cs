using System.Collections.Generic;
using Inventory.BranchInventory;
using Inventory.StockMovements;

namespace Inventory.Dashboard;

public class InventoryDashboardDto
{
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int TotalBranches { get; set; }
    public int ActiveBranches { get; set; }
    public int TotalCategories { get; set; }
    public int TotalSuppliers { get; set; }

    public int TotalInventoryItems { get; set; }
    public int HealthyStockCount { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }

    public StockMovementSummaryDto MovementSummary { get; set; } = new();
    public List<BranchDashboardStatsDto> BranchStats { get; set; } = new();
    public List<StockMovementDto> RecentMovements { get; set; } = new();
    public List<BranchInventoryDto> CriticalStockItems { get; set; } = new();
}
