using System;
using System.Collections.Generic;

namespace Production.Reports;

public class ProductionAnalyticsReadModel
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
    public int ForecastQuantity { get; set; }
    public int ActualDemandQuantity { get; set; }
    public decimal ForecastAccuracyPercent { get; set; }
    public decimal ForecastBiasPercent { get; set; }
    public List<ProductionDailyOutputPoint> DailyOutput { get; set; } = new();
    public List<ProductionProductFocusRow> ProductPerformance { get; set; } = new();
    public List<ProductionWasteReasonAnalyticsRow> WasteReasons { get; set; } = new();
    public List<ForecastAccuracyRow> ForecastAccuracy { get; set; } = new();
}

public class ProductionWasteReasonAnalyticsRow
{
    public string Reason { get; set; } = null!;
    public int IncidentCount { get; set; }
    public decimal Cost { get; set; }
}
