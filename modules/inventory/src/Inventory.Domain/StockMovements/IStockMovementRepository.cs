using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Volo.Abp.Domain.Repositories;

namespace Inventory.StockMovements;

public class StockMovementWithContext
{
    public AppStockMovement Movement { get; set; } = null!;
    public AppBranch Branch { get; set; } = null!;
    public AppProduct Product { get; set; } = null!;
}

public class StockMovementTypeCount
{
    public string MovementType { get; set; } = null!;
    public int Count { get; set; }
}

/// <summary>
/// One waste (WriteOff) aggregate per (branch, product, day): units written off and
/// their cost valued at the product's current CostPrice. Backs the Waste Analytics
/// page; the host service buckets the days into weeks and resolves display names.
/// </summary>
public class WasteAggregateRow
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }

    /// <summary>UTC day (date-precision) the write-off movements were recorded.</summary>
    public DateTime Date { get; set; }

    /// <summary>Units written off (positive — the movement deltas are negated).</summary>
    public int Units { get; set; }

    /// <summary>Units × the product's CostPrice.</summary>
    public decimal Cost { get; set; }
}

public class StockMovementListFilter
{
    public Guid? BranchId { get; set; }
    public Guid? ProductId { get; set; }
    public string? MovementType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Filter { get; set; }
    public IReadOnlyCollection<Guid>? BranchIdScope { get; set; }
}

public interface IStockMovementRepository : IRepository<AppStockMovement, Guid>
{
    Task<List<StockMovementWithContext>> GetRecentWithContextAsync(
        IReadOnlyCollection<Guid>? branchIdScope,
        DateTime since,
        CancellationToken cancellationToken = default);

    Task<long> CountFilteredAsync(
        StockMovementListFilter filter,
        CancellationToken cancellationToken = default);

    Task<List<StockMovementWithContext>> GetFilteredListAsync(
        StockMovementListFilter filter,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    Task<List<StockMovementTypeCount>> GetCountsByTypeAsync(
        StockMovementListFilter filter,
        CancellationToken cancellationToken = default);

    Task<StockMovementWithContext?> GetWithContextAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// WriteOff movements in [from, to) joined to their product's CostPrice and
    /// grouped by (branch, product, day). Quantities are returned positive.
    /// </summary>
    Task<List<WasteAggregateRow>> GetWriteOffAggregatesAsync(
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default);
}
