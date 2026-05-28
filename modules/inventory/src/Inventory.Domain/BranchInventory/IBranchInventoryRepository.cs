using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Volo.Abp.Domain.Repositories;

namespace Inventory.BranchInventory;

public interface IBranchInventoryRepository : IRepository<AppBranchInventory, Guid>
{
    Task<long> CountWithProductAsync(
        Guid branchId,
        string? filter,
        bool onlyOutOfStock,
        bool onlyLowStock,
        bool includeInactiveProducts,
        CancellationToken cancellationToken = default);

    Task<List<BranchInventoryWithProduct>> GetListWithProductAsync(
        Guid branchId,
        string? filter,
        bool onlyOutOfStock,
        bool onlyLowStock,
        bool includeInactiveProducts,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    Task<BranchInventoryWithProduct> GetWithProductAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<StockSnapshot>> GetActiveStockSnapshotsAsync(
        Guid branchId,
        CancellationToken cancellationToken = default);

    Task<List<InventoryStockRow>> GetActiveStockRowsAsync(
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Active products that currently have stock (QuantityOnHand &gt; 0) at the
    /// given branch, ordered by product name. Backs the "sellable products"
    /// lookup used when recording a sale.
    /// </summary>
    Task<List<InventoryStockRow>> GetAvailableProductsAsync(
        Guid branchId,
        CancellationToken cancellationToken = default);
}
