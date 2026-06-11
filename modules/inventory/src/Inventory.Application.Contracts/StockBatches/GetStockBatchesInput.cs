using System;
using Volo.Abp.Application.Dtos;

namespace Inventory.StockBatches;

public class GetStockBatchesInput : PagedAndSortedResultRequestDto
{
    /// <summary>Matches product name, product SKU or batch number.</summary>
    public string? Filter { get; set; }

    /// <summary>Optional single-branch filter (must be accessible to the caller).</summary>
    public Guid? BranchId { get; set; }

    /// <summary>Include fully consumed batches (QuantityRemaining == 0). Default false.</summary>
    public bool IncludeDepleted { get; set; }
}
