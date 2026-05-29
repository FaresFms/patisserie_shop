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
}
