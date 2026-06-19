using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Operations.Entities;

/// <summary>
/// A cash-drawer session for one cashier at one branch. Opened with a starting float,
/// closed by counting the drawer; Variance = counted − expected. Cash-only POS — there
/// is no payment-method concept anywhere in this aggregate.
/// Construction goes through <see cref="Operations.Cashiers.CashierShiftManager"/> so the
/// "one Open shift per cashier" rule (which needs a repository query) is enforced.
/// </summary>
public class AppCashierShift : FullAuditedAggregateRoot<Guid>
{
    public Guid BranchId { get; private set; }
    public Guid CashierUserId { get; private set; }
    public DateTime OpenedAt { get; private set; }
    public decimal OpeningFloat { get; private set; }
    public string Status { get; private set; } = CashierShiftStatuses.Open;

    public DateTime? ClosedAt { get; private set; }
    public decimal? CountedCash { get; private set; }
    public decimal? ExpectedCash { get; private set; }
    public decimal? Variance { get; private set; }

    protected AppCashierShift() { }

    internal AppCashierShift(
        Guid id,
        Guid branchId,
        Guid cashierUserId,
        DateTime openedAt,
        decimal openingFloat)
        : base(id)
    {
        if (openingFloat < 0)
        {
            throw new BusinessException(OperationsErrorCodes.InvalidOpeningFloat)
                .WithData("OpeningFloat", openingFloat);
        }

        BranchId = branchId;
        CashierUserId = cashierUserId;
        OpenedAt = openedAt;
        OpeningFloat = openingFloat;
        Status = CashierShiftStatuses.Open;
    }

    public bool IsOpen => Status == CashierShiftStatuses.Open;

    /// <summary>
    /// Closes the shift: records the counted drawer cash, the expected cash computed by
    /// the caller (opening float + non-voided sales − voided), and the variance between
    /// them. <paramref name="closedAt"/> defaults to the current UTC time; the app
    /// service passes the ABP IClock value.
    /// </summary>
    public void Close(decimal countedCash, decimal expectedCash, DateTime? closedAt = null)
    {
        if (Status != CashierShiftStatuses.Open)
        {
            throw new BusinessException(OperationsErrorCodes.ShiftAlreadyClosed)
                .WithData("ShiftId", Id);
        }

        CountedCash = countedCash;
        ExpectedCash = expectedCash;
        Variance = countedCash - expectedCash;
        ClosedAt = closedAt ?? DateTime.UtcNow;
        Status = CashierShiftStatuses.Closed;
    }
}
