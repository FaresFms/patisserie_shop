using System;
using Volo.Abp.Application.Dtos;

namespace Operations.StockTransfers;

public class GetStockTransfersInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public string? Status { get; set; }
    public Guid? FromBranchId { get; set; }
    public Guid? ToBranchId { get; set; }

    /// <summary>
    /// Perspective filter, resolved server-side against the current user:
    /// one of <see cref="StockTransferViews"/> (null/empty = all transfers).
    /// </summary>
    public string? View { get; set; }
}

/// <summary>Values accepted by <see cref="GetStockTransfersInput.View"/>.</summary>
public static class StockTransferViews
{
    /// <summary>Only transfers currently waiting for the current user's action.</summary>
    public const string NeedsMyAction = "action";
    /// <summary>Transfers coming TO a branch the current user manages.</summary>
    public const string Incoming = "incoming";
    /// <summary>Transfers going OUT of a branch the current user manages.</summary>
    public const string Outgoing = "outgoing";
}
