using System;
using System.Collections.Generic;

namespace Production.Dashboard;

public class ProductionDashboardDto
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
    public List<ProductionActionItemDto> ActionItems { get; set; } = new();
    public List<ProductionDailyOutputPointDto> OutputLast7Days { get; set; } = new();
    public List<ProductionProductFocusRowDto> ProductFocus { get; set; } = new();
}

public class ProductionActionItemDto
{
    public string Type { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Detail { get; set; } = null!;
    public string Url { get; set; } = null!;
    public int Count { get; set; }
}

public class ProductionDailyOutputPointDto
{
    public DateTime Date { get; set; }
    public int AcceptedQuantity { get; set; }
    public int RejectedQuantity { get; set; }
    public decimal TotalCost { get; set; }
}

public class ProductionProductFocusRowDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSku { get; set; } = null!;
    public int AcceptedQuantity { get; set; }
    public int RejectedQuantity { get; set; }
    public decimal YieldPercent { get; set; }
    public decimal UnitCost { get; set; }
}
