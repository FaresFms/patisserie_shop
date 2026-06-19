using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Operations.Cashiers;
using Operations.Entities;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Operations.EntityFrameworkCore;

public class CashierShiftRepository
    : EfCoreRepository<OperationsDbContext, AppCashierShift, Guid>,
      ICashierShiftRepository
{
    public CashierShiftRepository(IDbContextProvider<OperationsDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<AppCashierShift?> FindOpenShiftAsync(
        Guid branchId,
        Guid cashierUserId,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query
            .Where(s => s.BranchId == branchId
                        && s.CashierUserId == cashierUserId
                        && s.Status == CashierShiftStatuses.Open)
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<bool> HasOpenShiftAsync(
        Guid cashierUserId,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query
            .Where(s => s.CashierUserId == cashierUserId && s.Status == CashierShiftStatuses.Open)
            .AnyAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<ShiftSalesTotals> GetShiftSalesTotalsAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        // Aggregate the shift's linked sales server-side: count, non-voided sum, voided sum.
        var totals = await dbContext.Set<AppSale>()
            .Where(s => s.ShiftId == shiftId)
            .GroupBy(s => s.ShiftId)
            .Select(g => new ShiftSalesTotals
            {
                ShiftId = shiftId,
                SalesCount = g.Count(),
                NonVoidedTotal = g.Sum(s => s.IsVoided ? 0m : s.TotalAmount),
                VoidedTotal = g.Sum(s => s.IsVoided ? s.TotalAmount : 0m)
            })
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));

        return totals ?? new ShiftSalesTotals { ShiftId = shiftId };
    }

    public async Task<List<AppCashierShift>> GetListAsync(
        Guid? branchId,
        bool openOnly,
        IReadOnlyCollection<Guid>? branchIdScope,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();

        if (branchIdScope != null)
        {
            query = query.Where(s => branchIdScope.Contains(s.BranchId));
        }
        if (branchId.HasValue)
        {
            query = query.Where(s => s.BranchId == branchId.Value);
        }
        if (openOnly)
        {
            query = query.Where(s => s.Status == CashierShiftStatuses.Open);
        }

        return await query
            .OrderByDescending(s => s.OpenedAt)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
}
