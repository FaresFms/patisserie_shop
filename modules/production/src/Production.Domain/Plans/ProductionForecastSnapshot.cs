using System;

namespace Production.Plans;

public class ProductionForecastSnapshot
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSku { get; set; } = null!;
    public int ForecastQuantity { get; set; }
}
