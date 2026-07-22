using System;
using System.Collections.Generic;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Stocktakes;
using Operations.Sales;

namespace patisserie_shop.Analytics;

internal static class StocktakeReconciliationReportBuilder
{
    public static StocktakeReconciliationDto Build(
        int days,
        DateTime fromDate,
        DateTime toDate,
        List<DailySaleAggregate> dailySales,
        List<ProductSalesAggregate> productSales,
        StocktakeReconciliationSummary stocktakeSummary,
        List<StocktakeProductVarianceAggregate> productVariances,
        List<StocktakeReasonAggregate> reasonAggregates,
        List<InventoryStockRow> currentStock,
        List<AppProduct> products)
    {
        var productLookup = new Dictionary<Guid, AppProduct>();
        foreach (var product in products)
        {
            productLookup[product.Id] = product;
        }

        var rows = new Dictionary<Guid, StocktakeReconciliationProductDto>();
        var currentStockUnits = 0;
        foreach (var stock in currentStock)
        {
            var row = GetOrCreateRow(rows, stock.Product.Id, productLookup);
            row.CurrentStock += stock.Inventory.QuantityOnHand;
            currentStockUnits += stock.Inventory.QuantityOnHand;
        }

        var unitsSold = 0;
        foreach (var sale in productSales)
        {
            var row = GetOrCreateRow(rows, sale.ProductId, productLookup);
            row.QuantitySold = sale.TotalQuantitySold;
            row.Revenue = sale.TotalRevenue;
            unitsSold += sale.TotalQuantitySold;
        }

        var writeOffQuantity = 0;
        var unrecordedSaleQuantity = 0;
        foreach (var variance in productVariances)
        {
            var row = GetOrCreateRow(rows, variance.ProductId, productLookup);
            row.StocktakeCount = variance.SessionCount;
            row.DifferenceLineCount = variance.DifferenceLineCount;
            row.ShortageQuantity = variance.ShortageQuantity;
            row.OverageQuantity = variance.OverageQuantity;
            row.WriteOffQuantity = variance.WriteOffQuantity;
            row.UnrecordedSaleQuantity = variance.UnrecordedSaleQuantity;
            writeOffQuantity += variance.WriteOffQuantity;
            unrecordedSaleQuantity += variance.UnrecordedSaleQuantity;
        }

        var reportRows = new List<StocktakeReconciliationProductDto>();
        var estimatedShortageValue = 0m;
        var estimatedWriteOffValue = 0m;
        var repeatedVarianceProducts = 0;
        foreach (var row in rows.Values)
        {
            if (row.QuantitySold > 0 || row.DifferenceLineCount > 0)
            {
                reportRows.Add(row);
            }

            estimatedShortageValue += row.EstimatedShortageValue;
            estimatedWriteOffValue += row.EstimatedWriteOffValue;
            if (row.HasRepeatedVariance) repeatedVarianceProducts++;
        }
        reportRows.Sort(CompareRows);

        var attentionProducts = new List<StocktakeReconciliationProductDto>();
        foreach (var row in reportRows)
        {
            if (row.DifferenceLineCount == 0) continue;
            attentionProducts.Add(row);
            if (attentionProducts.Count == 5) break;
        }

        var totalRevenue = 0m;
        var totalSales = 0;
        foreach (var day in dailySales)
        {
            totalRevenue += day.TotalAmount;
            totalSales += day.SaleCount;
        }

        var reasons = new List<StocktakeReconciliationReasonDto>();
        foreach (var reason in reasonAggregates)
        {
            reasons.Add(new StocktakeReconciliationReasonDto
            {
                Reason = reason.Reason,
                LineCount = reason.LineCount,
                ShortageQuantity = reason.ShortageQuantity,
                OverageQuantity = reason.OverageQuantity
            });
        }

        return new StocktakeReconciliationDto
        {
            Days = days,
            FromDate = fromDate,
            ToDate = toDate,
            TotalRevenue = totalRevenue,
            TotalSales = totalSales,
            UnitsSold = unitsSold,
            CurrentStockUnits = currentStockUnits,
            CompletedStocktakes = stocktakeSummary.CompletedSessionCount,
            ProductsWithVariance = productVariances.Count,
            ShortageQuantity = stocktakeSummary.ShortageQuantity,
            OverageQuantity = stocktakeSummary.OverageQuantity,
            WriteOffQuantity = writeOffQuantity,
            UnrecordedSaleQuantity = unrecordedSaleQuantity,
            EstimatedShortageValue = estimatedShortageValue,
            EstimatedWriteOffValue = estimatedWriteOffValue,
            RepeatedVarianceProducts = repeatedVarianceProducts,
            Products = reportRows,
            AttentionProducts = attentionProducts,
            Reasons = reasons
        };
    }

    private static StocktakeReconciliationProductDto GetOrCreateRow(
        IDictionary<Guid, StocktakeReconciliationProductDto> rows,
        Guid productId,
        IReadOnlyDictionary<Guid, AppProduct> products)
    {
        if (rows.TryGetValue(productId, out var row)) return row;

        products.TryGetValue(productId, out var product);
        row = new StocktakeReconciliationProductDto
        {
            ProductId = productId,
            ProductName = product?.Name ?? productId.ToString("N")[..8].ToUpperInvariant(),
            Sku = product?.SKU ?? string.Empty,
            Unit = product?.Unit ?? string.Empty,
            CostPrice = product?.CostPrice ?? 0m
        };
        rows[productId] = row;
        return row;
    }

    private static int CompareRows(
        StocktakeReconciliationProductDto left,
        StocktakeReconciliationProductDto right)
    {
        var comparison = right.AttentionScore.CompareTo(left.AttentionScore);
        if (comparison != 0) return comparison;
        comparison = right.UnrecordedSaleQuantity.CompareTo(left.UnrecordedSaleQuantity);
        if (comparison != 0) return comparison;
        comparison = right.EstimatedShortageValue.CompareTo(left.EstimatedShortageValue);
        if (comparison != 0) return comparison;
        return right.QuantitySold.CompareTo(left.QuantitySold);
    }
}
