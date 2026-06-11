using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Intelligence.Entities;
using Intelligence.Velocity;
using Inventory.BranchInventory;
using Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Intelligence.EntityFrameworkCore;

/// <summary>
/// Implements the velocity × product × branch × branch-inventory read model.
/// AppProductVelocity lives in the Intelligence DbContext while products, branches
/// and branch inventory live in the Inventory DbContext, so a single server-side
/// join is not possible; instead the velocity rows are filtered server-side and
/// overlaid in memory with the same public Inventory APIs the scanners use
/// (GetActiveStockRowsAsync). The data set is bounded by product×branch, which is
/// small for this system. Filter / sort-key translation / paging all live here so
/// the app service stays LINQ-free.
/// </summary>
public class ProductVelocityRepository
    : EfCoreRepository<IntelligenceDbContext, AppProductVelocity, Guid>,
      IProductVelocityRepository
{
    private readonly IBranchInventoryRepository _branchInventoryRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;

    public ProductVelocityRepository(
        IDbContextProvider<IntelligenceDbContext> dbContextProvider,
        IBranchInventoryRepository branchInventoryRepository,
        IRepository<AppBranch, Guid> branchRepository)
        : base(dbContextProvider)
    {
        _branchInventoryRepository = branchInventoryRepository;
        _branchRepository = branchRepository;
    }

    public async Task<long> CountFilteredAsync(
        string? filter,
        Guid? branchId,
        IReadOnlyCollection<Guid>? scopedBranchIds,
        CancellationToken cancellationToken = default)
    {
        var rows = await BuildJoinedRowsAsync(filter, branchId, scopedBranchIds, cancellationToken);
        return rows.Count;
    }

    public async Task<List<ProductVelocityListRow>> GetFilteredListAsync(
        string? filter,
        Guid? branchId,
        IReadOnlyCollection<Guid>? scopedBranchIds,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var rows = await BuildJoinedRowsAsync(filter, branchId, scopedBranchIds, cancellationToken);

        return ApplySorting(rows, sorting)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToList();
    }

    private async Task<List<ProductVelocityListRow>> BuildJoinedRowsAsync(
        string? filter,
        Guid? branchId,
        IReadOnlyCollection<Guid>? scopedBranchIds,
        CancellationToken cancellationToken)
    {
        var ct = GetCancellationToken(cancellationToken);

        var query = await GetQueryableAsync();

        if (branchId.HasValue)
        {
            query = query.Where(v => v.BranchId == branchId.Value);
        }

        if (scopedBranchIds != null)
        {
            var ids = scopedBranchIds.ToArray();
            query = query.Where(v => ids.Contains(v.BranchId));
        }

        var velocities = await query.ToListAsync(ct);
        if (velocities.Count == 0)
        {
            return new List<ProductVelocityListRow>();
        }

        // Active-product inventory rows with their product (same API the scanners use).
        var stockRows = await _branchInventoryRepository.GetActiveStockRowsAsync(scopedBranchIds, ct);
        var stockByKey = new Dictionary<(Guid ProductId, Guid BranchId), InventoryStockRow>();
        foreach (var row in stockRows)
        {
            stockByKey[(row.Inventory.ProductId, row.Inventory.BranchId)] = row;
        }

        var branches = await _branchRepository.GetListAsync(cancellationToken: ct);
        var branchNameById = branches.ToDictionary(b => b.Id, b => b.Name);

        var f = string.IsNullOrWhiteSpace(filter) ? null : filter.Trim().ToLowerInvariant();

        var result = new List<ProductVelocityListRow>(velocities.Count);
        foreach (var velocity in velocities)
        {
            // Inner join: drop velocity rows whose product is no longer active /
            // whose inventory row disappeared — there is nothing meaningful to show.
            if (!stockByKey.TryGetValue((velocity.ProductId, velocity.BranchId), out var stockRow))
            {
                continue;
            }

            var productName = stockRow.Product.Name;
            var productSku = stockRow.Product.SKU;

            if (f != null
                && !productName.ToLowerInvariant().Contains(f)
                && !productSku.ToLowerInvariant().Contains(f))
            {
                continue;
            }

            var currentStock = stockRow.Inventory.QuantityOnHand;

            result.Add(new ProductVelocityListRow
            {
                Velocity = velocity,
                ProductName = productName,
                ProductSku = productSku,
                BranchName = branchNameById.TryGetValue(velocity.BranchId, out var branchName)
                    ? branchName
                    : velocity.BranchId.ToString(),
                CurrentStock = currentStock,
                DaysOfCover = velocity.AvgDailySales30 > 0
                    ? Math.Round(currentStock / velocity.AvgDailySales30, 1)
                    : null
            });
        }

        return result;
    }

    /// <summary>
    /// Translates the public DTO sort keys to in-memory orderings. Default:
    /// AbcClass asc, Revenue30 desc (the "most important products first" view).
    /// </summary>
    private static IEnumerable<ProductVelocityListRow> ApplySorting(
        List<ProductVelocityListRow> rows,
        string sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
        {
            return rows
                .OrderBy(r => r.Velocity.AbcClass, StringComparer.Ordinal)
                .ThenByDescending(r => r.Velocity.Revenue30);
        }

        var parts = sorting.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var field = parts[0];
        var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        Func<ProductVelocityListRow, object?> keySelector = field.ToLowerInvariant() switch
        {
            "productname" => r => r.ProductName,
            "productsku" => r => r.ProductSku,
            "branchname" => r => r.BranchName,
            "abcclass" => r => r.Velocity.AbcClass,
            "avgdailysales7" => r => r.Velocity.AvgDailySales7,
            "avgdailysales30" => r => r.Velocity.AvgDailySales30,
            "quantitysold30" => r => r.Velocity.QuantitySold30,
            "revenue30" => r => r.Velocity.Revenue30,
            "currentstock" => r => r.CurrentStock,
            "daysofcover" => r => r.DaysOfCover ?? decimal.MaxValue, // "no velocity" sorts as infinite cover
            "computedatutc" => r => r.Velocity.ComputedAtUtc,
            _ => r => r.Velocity.AbcClass
        };

        return descending
            ? rows.OrderByDescending(keySelector)
            : rows.OrderBy(keySelector);
    }
}
