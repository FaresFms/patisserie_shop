using System;
using Operations;
using Operations.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace patisserie_shop.Operations;

/// <summary>
/// Pure unit tests for the cash-drawer aggregate and the sale void state used by the
/// cashier POS: close variance math, the already-closed / already-voided guards, the
/// non-negative opening float, and the shift link.
/// </summary>
public class AppCashierShiftTests
{
    // AppCashierShift's ctor is internal (constructed only via CashierShiftManager in
    // production). For a pure unit test we invoke it via reflection and unwrap the
    // TargetInvocationException so domain BusinessExceptions surface directly.
    private static AppCashierShift NewOpenShift(decimal openingFloat = 100m)
    {
        try
        {
            return (AppCashierShift)Activator.CreateInstance(
                typeof(AppCashierShift),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                args: new object[]
                {
                    Guid.NewGuid(),
                    Guid.NewGuid(),          // branchId
                    Guid.NewGuid(),          // cashierUserId
                    new DateTime(2026, 6, 18, 9, 0, 0, DateTimeKind.Utc),
                    openingFloat
                },
                culture: null)!;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    [Fact]
    public void Constructor_Should_Reject_Negative_Opening_Float()
    {
        Should.Throw<BusinessException>(() => NewOpenShift(openingFloat: -1m))
            .Code.ShouldBe(OperationsErrorCodes.InvalidOpeningFloat);
    }

    [Fact]
    public void Close_Should_Set_Variance_As_Counted_Minus_Expected_And_Flip_Status()
    {
        var shift = NewOpenShift();
        var closedAt = new DateTime(2026, 6, 18, 17, 0, 0, DateTimeKind.Utc);

        // Opening float 100 + non-voided sales 250 → expected 350; counted 345 → short by 5.
        shift.Close(countedCash: 345m, expectedCash: 350m, closedAt: closedAt);

        shift.Status.ShouldBe(CashierShiftStatuses.Closed);
        shift.IsOpen.ShouldBeFalse();
        shift.CountedCash.ShouldBe(345m);
        shift.ExpectedCash.ShouldBe(350m);
        shift.Variance.ShouldBe(-5m);
        shift.ClosedAt.ShouldBe(closedAt);
    }

    [Fact]
    public void Close_Should_Throw_When_Already_Closed()
    {
        var shift = NewOpenShift();
        shift.Close(countedCash: 100m, expectedCash: 100m);

        Should.Throw<BusinessException>(() => shift.Close(countedCash: 120m, expectedCash: 100m))
            .Code.ShouldBe(OperationsErrorCodes.ShiftAlreadyClosed);
    }
}
