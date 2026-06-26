using System;
using System.Collections.Generic;

namespace Production.Waste;

public class GetProductionWasteAnalyticsInput
{
    public int Days { get; set; } = 30;
    public Guid? KitchenBranchId { get; set; }
}

public class ProductionWasteAnalyticsDto
{
    public ProductionWasteSummaryDto Summary { get; set; } = new();
    public List<ProductionWasteDailyPointDto> DailySeries { get; set; } = new();
    public List<ProductionWasteReasonSliceDto> Reasons { get; set; } = new();
    public List<ProductionWasteProductRowDto> TopProducts { get; set; } = new();
}

public class ProductionWasteSummaryDto
{
    public int TotalQuantity { get; set; }
    public decimal TotalCost { get; set; }
    public string? TopReason { get; set; }
    public string? TopProductName { get; set; }
}

public class ProductionWasteDailyPointDto
{
    public DateTime Date { get; set; }
    public int Quantity { get; set; }
    public decimal Cost { get; set; }
}

public class ProductionWasteReasonSliceDto
{
    public string Reason { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal Cost { get; set; }
}

public class ProductionWasteProductRowDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSku { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal Cost { get; set; }
}
