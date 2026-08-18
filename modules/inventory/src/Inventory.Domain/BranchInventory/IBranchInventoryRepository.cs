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
        bool onlySellable = false,
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
        bool onlySellable = false,
        CancellationToken cancellationToken = default);

    Task<BranchInventoryWithProduct> GetWithProductAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<BranchInventoryWithProduct?> FindByBranchAndProductAsync(
        Guid branchId,
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<List<BranchInventoryWithProduct>> GetStocktakeRowsAsync(
        Guid branchId,
        CancellationToken cancellationToken = default);

    Task<List<StockSnapshot>> GetActiveStockSnapshotsAsync(
        Guid branchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inventory rows for a single product across branches, joined with the branch
    /// so the caller can render a cross-branch stock view. Ordered by branch name.
    /// When <paramref name="branchIdScope"/> is non-null, restricts to those branches
    /// (branch-isolation for managers); null returns every branch (ManageAll callers).
    /// </summary>
    Task<List<ProductBranchStockRow>> GetByProductAsync(
        Guid productId,
        IReadOnlyCollection<Guid>? branchIdScope = null,
        CancellationToken cancellationToken = default);

    Task<List<InventoryStockRow>> GetActiveStockRowsAsync(
        IReadOnlyCollection<Guid>? branchIdScope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One row per active candidate branch and requested product. The destination
    /// is excluded and missing inventory is represented by zero stock.
    /// </summary>
    Task<List<TransferSourceStockRow>> GetTransferSourceStockAsync(
        Guid destinationBranchId,
        IReadOnlyCollection<Guid> productIds,
        IReadOnlyCollection<Guid>? sourceBranchIdScope = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Active products that currently have stock (QuantityOnHand &gt; 0) at the
    /// given branch, ordered by product name. Backs the "available products"
    /// lookup used when recording a sale or a transfer. When <paramref name="onlySellable"/>
    /// is true, restricts to products flagged IsSellable (POS / sales path only).
    /// </summary>
    Task<List<InventoryStockRow>> GetAvailableProductsAsync(
        Guid branchId,
        bool onlySellable = false,
        CancellationToken cancellationToken = default);
}
