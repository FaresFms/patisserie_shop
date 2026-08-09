using System;
using System.Collections.Generic;

namespace Production.Waste;

public class ProductionWasteListItem
{
    public Guid Id { get; set; }
    public Guid? ProductionOrderId { get; set; }
    public string? ProductionOrderNumber { get; set; }
    public Guid KitchenBranchId { get; set; }
    public string KitchenBranchName { get; set; } = null!;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSku { get; set; } = null!;
    public string ProductUnit { get; set; } = null!;
    public string WasteType { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string Reason { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class ProductionWasteSummary
{
    public int TotalIncidents { get; set; }
    public decimal TotalCost { get; set; }
    public string? TopReason { get; set; }
    public string? TopProductName { get; set; }
}

public class ProductionWasteReasonSlice
{
    public string Reason { get; set; } = null!;
    public int IncidentCount { get; set; }
    public decimal Cost { get; set; }
}

public class ProductionWasteProductRow
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSku { get; set; } = null!;
    public string ProductUnit { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal Cost { get; set; }
}

public class ProductionWasteDailyPoint
{
    public DateTime Date { get; set; }
    public int IncidentCount { get; set; }
    public decimal Cost { get; set; }
}

public class ProductionWasteAnalyticsReadModel
{
    public ProductionWasteSummary Summary { get; set; } = new();
    public List<ProductionWasteDailyPoint> DailySeries { get; set; } = new();
    public List<ProductionWasteReasonSlice> Reasons { get; set; } = new();
    public List<ProductionWasteProductRow> TopProducts { get; set; } = new();
}
