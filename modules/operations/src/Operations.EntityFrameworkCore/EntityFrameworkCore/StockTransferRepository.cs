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
        List<Guid>? fromBranchIdIn = null,
        List<Guid>? toBranchIdIn = null,
        StockTransferVisibilitySpec? visibility = null,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(status, fromBranchId, toBranchId, filter, fromBranchIdIn, toBranchIdIn, visibility);
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
        List<Guid>? fromBranchIdIn = null,
        List<Guid>? toBranchIdIn = null,
        StockTransferVisibilitySpec? visibility = null,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(status, fromBranchId, toBranchId, filter, fromBranchIdIn, toBranchIdIn, visibility);

        var rows = query
            .OrderBy(ResolveSorting(sorting))
            .Skip(skipCount)
            .Take(maxResultCount)
            .Select(t => new StockTransferListRow { Transfer = t, ItemCount = t.Items.Count });

        return await rows.ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<long> CountActionRequiredAsync(
        StockTransferActionSpec spec,
        string? status,
        Guid? fromBranchId,
        Guid? toBranchId,
        string? filter,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(status, fromBranchId, toBranchId, filter);
        return await ApplyActionFilter(query, spec)
            .LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<StockTransferListRow>> GetActionRequiredListAsync(
        StockTransferActionSpec spec,
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

        var rows = ApplyActionFilter(query, spec)
            .OrderBy(ResolveSorting(sorting))
            .Skip(skipCount)
            .Take(maxResultCount)
            .Select(t => new StockTransferListRow { Transfer = t, ItemCount = t.Items.Count });

        return await rows.ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<StockTransferActionCounts> GetActionCountsAsync(
        StockTransferActionSpec spec,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        var token = GetCancellationToken(cancellationToken);
        var managed = spec.ManagedBranchIds ?? new List<Guid>();

        return new StockTransferActionCounts
        {
            DraftsToSubmit = await query.CountAsync(t =>
                t.Status == StockTransferStatuses.Draft
                && ((spec.UserId != null && t.RequestedByUserId == spec.UserId)
                    || (!spec.ManageAllBranches && managed.Contains(t.ToBranchId))), token),

            PendingToApprove = spec.CanApprove
                ? await query.CountAsync(t => t.Status == StockTransferStatuses.Pending, token)
                : 0,

            ApprovedToShip = await query.CountAsync(t =>
                t.Status == StockTransferStatuses.Approved
                && t.FromBranchId.HasValue
                && (spec.ManageAllBranches || managed.Contains(t.FromBranchId.Value)), token),

            InTransitToReceive = await query.CountAsync(t =>
                t.Status == StockTransferStatuses.InTransit
                && (spec.ManageAllBranches || managed.Contains(t.ToBranchId)), token)
        };
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
        string? filter,
        List<Guid>? fromBranchIdIn = null,
        List<Guid>? toBranchIdIn = null,
        StockTransferVisibilitySpec? visibility = null)
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

        if (fromBranchIdIn != null)
        {
            query = query.Where(t => t.FromBranchId.HasValue && fromBranchIdIn.Contains(t.FromBranchId.Value));
        }

        if (toBranchIdIn != null)
        {
            query = query.Where(t => toBranchIdIn.Contains(t.ToBranchId));
        }

        if (visibility is { CanViewAll: false })
        {
            var managed = visibility.ManagedBranchIds;
            query = query.Where(t => managed.Contains(t.ToBranchId)
                || (t.FromBranchId.HasValue && managed.Contains(t.FromBranchId.Value)));
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(t => t.Notes != null && t.Notes.ToLower().Contains(f));
        }

        return query;
    }

    private static IQueryable<AppStockTransfer> ApplyActionFilter(
        IQueryable<AppStockTransfer> query,
        StockTransferActionSpec spec)
    {
        var managed = spec.ManagedBranchIds ?? new List<Guid>();

        return query.Where(t =>
            (t.Status == StockTransferStatuses.Draft
                && ((spec.UserId != null && t.RequestedByUserId == spec.UserId)
                    || (!spec.ManageAllBranches && managed.Contains(t.ToBranchId))))
            || (spec.CanApprove && t.Status == StockTransferStatuses.Pending)
            || (t.Status == StockTransferStatuses.Approved
                && t.FromBranchId.HasValue
                && (spec.ManageAllBranches || managed.Contains(t.FromBranchId.Value)))
            || (t.Status == StockTransferStatuses.InTransit
                && (spec.ManageAllBranches || managed.Contains(t.ToBranchId))));
    }

    private static readonly string[] SortableColumns =
    {
        nameof(AppStockTransfer.RequestedDate),
        nameof(AppStockTransfer.ApprovedDate),
        nameof(AppStockTransfer.ShippedDate),
        nameof(AppStockTransfer.CompletedDate),
        nameof(AppStockTransfer.Status),
        nameof(AppStockTransfer.CreationTime),
        nameof(AppStockTransfer.FromBranchId),
        nameof(AppStockTransfer.ToBranchId)
    };

    private static string ResolveSorting(string? sorting)
    {
        const string fallback = $"{nameof(AppStockTransfer.RequestedDate)} desc";
        if (string.IsNullOrWhiteSpace(sorting))
            return fallback;

        var s = sorting.Trim();
        // Display-name columns can't be sorted at the DB level (different DbContexts) —
        // fall back to the corresponding FK column so the grid stays usable.
        if (s.StartsWith("FromBranchName", StringComparison.OrdinalIgnoreCase))
            s = s.Replace("FromBranchName", nameof(AppStockTransfer.FromBranchId), StringComparison.OrdinalIgnoreCase);
        if (s.StartsWith("ToBranchName", StringComparison.OrdinalIgnoreCase))
            s = s.Replace("ToBranchName", nameof(AppStockTransfer.ToBranchId), StringComparison.OrdinalIgnoreCase);

        // Whitelist the column name — MudBlazor template columns (and any tampered
        // client input) send strings Dynamic LINQ would otherwise throw on.
        var column = s.Split(' ')[0];
        return SortableColumns.Contains(column, StringComparer.OrdinalIgnoreCase) ? s : fallback;
    }
}
