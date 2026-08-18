using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Operations.StockTransfers;

public class GetStockTransferSourceSuggestionsInput
{
    public Guid ToBranchId { get; set; }
    public List<StockTransferSourceRequestItemDto> Items { get; set; } = new();
}

public class StockTransferSourceRequestItemDto
{
    public Guid ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int RequestedQuantity { get; set; }
}

public class StockTransferSourceSuggestionDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public bool IsRecommended { get; set; }
    public bool CanFulfillAll { get; set; }
    public int CoveredItemCount { get; set; }
    public int TotalItemCount { get; set; }
    public int CoveragePercent { get; set; }
    public int TotalSafeAvailable { get; set; }
    public int TotalMissingQuantity { get; set; }
    public List<StockTransferSourceProductSuggestionDto> Products { get; set; } = new();
}

public class StockTransferSourceProductSuggestionDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public int QuantityOnHand { get; set; }
    public int MinimumStock { get; set; }
    public int SafeAvailableQuantity { get; set; }
    public int CoveredQuantity { get; set; }
    public int MissingQuantity { get; set; }
}
