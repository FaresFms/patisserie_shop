using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Operations.Entities;
using Operations.StockTransfers;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Operations.EntityFrameworkCore;

public class StockTransferRepository
    : EfCoreRepository<OperationsDbContext, AppStockTransfer, Guid>,
      IStockTransferRepository
{
    public StockTransferRepository(IDbContextProvider<OperationsDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<AppStockTransfer> GetWithItemsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = await WithDetailsAsync(t => t.Items);
        var transfer = await query
            .Where(t => t.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));

        if (transfer == null)
        {
            throw new EntityNotFoundException(typeof(AppStockTransfer), id);
        }
        return transfer;
    }

    public async Task<long> CountFilteredAsync(
        string? status,
        Guid? fromBranchId,
        Guid? toBranchId,
        string? filter,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(status, fromBranchId, toBranchId, filter);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<StockTransferListRow>> GetFilteredListAsync(
        string? status,
        Guid? fromBranchId,
        Guid? toBranchId,
        string? filter,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(status, fromBranchId, toBranchId, filter);

        var rows = query
            .OrderBy(ResolveSorting(sorting))
            .Skip(skipCount)
            .Take(maxResultCount)
            .Select(t => new StockTransferListRow { Transfer = t, ItemCount = t.Items.Count });

        return await rows.ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<StockTransferListRow>> GetActiveIncomingAsync(
        Guid toBranchId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        var active = new[]
        {
            StockTransferStatuses.Pending,
            StockTransferStatuses.Approved,
            StockTransferStatuses.InTransit
        };

        var rows = query
            .Where(t => t.ToBranchId == toBranchId && active.Contains(t.Status))
            .OrderByDescending(t => t.RequestedDate)
            .Take(take)
            .Select(t => new StockTransferListRow { Transfer = t, ItemCount = t.Items.Count });

        return await rows.ToListAsync(GetCancellationToken(cancellationToken));
    }

    private async Task<IQueryable<AppStockTransfer>> BuildFilteredQueryAsync(
        string? status,
        Guid? fromBranchId,
        Guid? toBranchId,
        string? filter)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(status) && StockTransferStatuses.All.Contains(status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (fromBranchId.HasValue)
        {
            query = query.Where(t => t.FromBranchId.HasValue && t.FromBranchId.Value == fromBranchId.Value);
        }

        if (toBranchId.HasValue)
        {
            query = query.Where(t => t.ToBranchId == toBranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(t => t.Notes != null && t.Notes.ToLower().Contains(f));
        }

        return query;
    }

    private static string ResolveSorting(string? sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
            return $"{nameof(AppStockTransfer.RequestedDate)} desc";

        var s = sorting.Trim();
        // Display-name columns can't be sorted at the DB level (different DbContexts) —
        // fall back to the corresponding FK column so the grid stays usable.
        if (s.StartsWith("FromBranchName", StringComparison.OrdinalIgnoreCase))
            return s.Replace("FromBranchName", nameof(AppStockTransfer.FromBranchId), StringComparison.OrdinalIgnoreCase);
        if (s.StartsWith("ToBranchName", StringComparison.OrdinalIgnoreCase))
            return s.Replace("ToBranchName", nameof(AppStockTransfer.ToBranchId), StringComparison.OrdinalIgnoreCase);
        return s;
    }
}
