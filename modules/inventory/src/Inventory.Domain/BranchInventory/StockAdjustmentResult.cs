using System.Collections.Generic;
using Inventory.Entities;
using Inventory.StockBatches;

namespace Inventory.BranchInventory;

public sealed record StockAdjustmentResult(
    AppStockMovement Movement,
    IReadOnlyList<ConsumedStockBatchLine> ConsumedBatches);
