using System;
using Inventory.Entities;

namespace Inventory.Stocktakes;

public sealed record StocktakeSessionLineSeed(
    Guid InventoryId,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    string ProductUnit,
    int ExpectedQuantity,
    bool IsPerishable,
    string InventoryConcurrencyStamp);

public sealed record StocktakeSessionDraftLine(
    Guid LineId,
    int? CountedQuantity,
    string? Reason,
    string? ReasonNotes,
    DateTime? ProductionDate);

public sealed record StocktakeSessionStartResult(AppStocktakeSession Session, bool IsNew);
