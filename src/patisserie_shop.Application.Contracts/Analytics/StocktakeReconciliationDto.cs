using System;
using System.Collections.Generic;

namespace patisserie_shop.Analytics;

public class StocktakeReconciliationDto
{
    public int Days { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalSales { get; set; }
    public int UnitsSold { get; set; }
    public int CurrentStockUnits { get; set; }
    public int CompletedStocktakes { get; set; }
    public int ProductsWithVariance { get; set; }
    public int ShortageQuantity { get; set; }
    public int OverageQuantity { get; set; }
    public int WriteOffQuantity { get; set; }
    public int UnrecordedSaleQuantity { get; set; }
    public decimal EstimatedShortageValue { get; set; }
    public decimal EstimatedWriteOffValue { get; set; }
    public int RepeatedVarianceProducts { get; set; }
    public List<StocktakeReconciliationProductDto> Products { get; set; } = new();
    public List<StocktakeReconciliationProductDto> AttentionProducts { get; set; } = new();
    public List<StocktakeReconciliationReasonDto> Reasons { get; set; } = new();
}

public class StocktakeReconciliationProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public int CurrentStock { get; set; }
    public int QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public int StocktakeCount { get; set; }
    public int DifferenceLineCount { get; set; }
    public int ShortageQuantity { get; set; }
    public int OverageQuantity { get; set; }
    public int NetVariance => OverageQuantity - ShortageQuantity;
    public int WriteOffQuantity { get; set; }
    public int UnrecordedSaleQuantity { get; set; }
    public decimal EstimatedShortageValue => CostPrice * ShortageQuantity;
    public decimal EstimatedWriteOffValue => CostPrice * WriteOffQuantity;
    public decimal EstimatedUnrecordedSaleValue => CostPrice * UnrecordedSaleQuantity;
    public bool HasRepeatedVariance => StocktakeCount >= 2;
    public decimal ShortageRate => QuantitySold + ShortageQuantity == 0
        ? 0m
        : Math.Round((decimal)ShortageQuantity * 100m / (QuantitySold + ShortageQuantity), 1);
    public int AttentionScore =>
        (UnrecordedSaleQuantity * 5)
        + (WriteOffQuantity * 4)
        + (ShortageQuantity * 2)
        + (HasRepeatedVariance ? StocktakeCount * 3 : 0);
}

public class StocktakeReconciliationReasonDto
{
    public string Reason { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public int ShortageQuantity { get; set; }
    public int OverageQuantity { get; set; }
    public int TotalQuantity => ShortageQuantity + OverageQuantity;
}
