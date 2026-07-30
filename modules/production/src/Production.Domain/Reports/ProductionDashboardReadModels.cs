using System;
using System.Collections.Generic;

namespace Production.Reports;

public class ProductionDashboardReadModel
{
    public int PendingRequests { get; set; }
    public int UnfulfilledDueToday { get; set; }
    public int WaitingForIngredients { get; set; }
    public int ReadyToCook { get; set; }
    public int InProduction { get; set; }
    public int CompletedToday { get; set; }
    public int AcceptedToday { get; set; }
    public int RejectedToday { get; set; }
    public decimal WasteCostToday { get; set; }
    public decimal YieldPercentToday { get; set; }
    public decimal FulfillmentPercent { get; set; }
    public List<ProductionActionItemReadModel> ActionItems { get; set; } = new();
    public List<ProductionDailyOutputPoint> OutputLast7Days { get; set; } = new();
    public List<ProductionProductFocusRow> ProductFocus { get; set; } = new();
}

public class ProductionActionItemReadModel
{
    public string Type { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string Url { get; set; } = null!;
    public int Count { get; set; }
}

public class ProductionDailyOutputPoint
{
    public DateTime Date { get; set; }
    public int AcceptedQuantity { get; set; }
    public int RejectedQuantity { get; set; }
    public decimal TotalCost { get; set; }
}

public class ProductionProductFocusRow
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSku { get; set; } = null!;
    public int AcceptedQuantity { get; set; }
    public int RejectedQuantity { get; set; }
    public decimal YieldPercent { get; set; }
    public decimal UnitCost { get; set; }
}
