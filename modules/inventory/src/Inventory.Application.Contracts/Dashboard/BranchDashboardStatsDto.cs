using System;

namespace Inventory.Dashboard;

public class BranchDashboardStatsDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = null!;
    public bool IsActive { get; set; }
    public int TotalItems { get; set; }
    public int HealthyStockCount { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public double HealthPercentage => TotalItems > 0 ? Math.Round((double)HealthyStockCount / TotalItems * 100, 1) : 0;
}
