using Inventory.Entities;

namespace Inventory.StockBatches;

/// <summary>Batch + its product, for the expiry scanner (branch is resolved separately).</summary>
public class StockBatchWithProduct
{
    public AppStockBatch Batch { get; set; } = null!;
    public AppProduct Product { get; set; } = null!;
}

/// <summary>Batch + product + branch, for the Stock Batches list page.</summary>
public class StockBatchWithDetails
{
    public AppStockBatch Batch { get; set; } = null!;
    public AppProduct Product { get; set; } = null!;
    public AppBranch Branch { get; set; } = null!;
}
