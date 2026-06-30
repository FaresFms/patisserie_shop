using System;

namespace Production.Analytics;

public class GetProductionAnalyticsInput
{
    public int Days { get; set; } = 30;
    public Guid? KitchenBranchId { get; set; }
}
