using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Operations.Entities;
using Volo.Abp.Domain.Repositories;

namespace Operations.Cashiers;

/// <summary>
/// Custom repository for <see cref="AppCashierShift"/>. Owns the shift listing/filtering
/// and the per-shift sales aggregation (count, non-voided total, voided total) so the
/// app service never touches a queryable to compute drawer figures.
/// </summary>
public interface ICashierShiftRepository : IRepository<AppCashierShift, Guid>
{
    /// <summary>The cashier's currently Open shift at the branch, or null.</summary>
    Task<AppCashierShift?> FindOpenShiftAsync(
        Guid branchId,
        Guid cashierUserId,
        CancellationToken cancellationToken = default);

    /// <summary>True when the cashier already has any Open shift (at any branch).</summary>
    Task<bool> HasOpenShiftAsync(
        Guid cashierUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sales count + non-voided total + voided total for one shift, grouped in the query.
    /// Returns zeros if the shift has no linked sales.
    /// </summary>
    Task<ShiftSalesTotals> GetShiftSalesTotalsAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default);

    /// <summary>Filtered + paged shift list for the manager drawer view.</summary>
    Task<List<AppCashierShift>> GetListAsync(
        Guid? branchId,
        bool openOnly,
        IReadOnlyCollection<Guid>? branchIdScope,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);
}
