using Operations.Entities;

namespace Operations.StockTransfers;

/// <summary>
/// Typed read-model returned by <see cref="IStockTransferRepository"/> list queries:
/// the transfer aggregate plus its line-item count, aggregated in the query.
/// </summary>
public class StockTransferListRow
{
    public AppStockTransfer Transfer { get; set; } = null!;
    public int ItemCount { get; set; }
}
