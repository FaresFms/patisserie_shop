using System.Collections.Generic;
using Production.Dashboard;

namespace Production.Analytics;

public class ProductionAnalyticsDto
{
    public int Days { get; set; }
    public int PlannedQuantity { get; set; }
    public int AcceptedQuantity { get; set; }
    public int RejectedQuantity { get; set; }
    public int ApprovedRequestQuantity { get; set; }
    public int FulfilledRequestQuantity { get; set; }
    public decimal YieldPercent { get; set; }
    public decimal WastePercent { get; set; }
    public decimal FulfillmentPercent { get; set; }
    public decimal PlannedCost { get; set; }
    public decimal ActualCost { get; set; }
    public decimal CostVariance { get; set; }
    public decimal CostVariancePercent { get; set; }
    public decimal AverageUnitCost { get; set; }
    public List<ProductionDailyOutputPointDto> DailyOutput { get; set; } = new();
    public List<ProductionProductFocusRowDto> ProductPerformance { get; set; } = new();
    public List<ProductionWasteReasonAnalyticsRowDto> WasteReasons { get; set; } = new();
}

public class ProductionWasteReasonAnalyticsRowDto
{
    public string Reason { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal Cost { get; set; }
}
