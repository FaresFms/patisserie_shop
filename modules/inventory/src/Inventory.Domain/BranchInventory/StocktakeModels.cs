using System;

namespace Inventory.BranchInventory;

public sealed record StocktakeCount(
    Guid InventoryId,
    int CountedQuantity,
    string? ConcurrencyStamp,
    string? Reason,
    string? ReasonNotes,
    DateTime? ProductionDate);

public sealed record StocktakePostingResult(
    Guid ReferenceId,
    int CountedLineCount,
    int AdjustedLineCount,
    int MatchedLineCount,
    int WriteOffLineCount,
    int ManualAdjustmentLineCount,
    int ShortageQuantity,
    int OverageQuantity);
