using System;
using System.Threading.Tasks;
using Operations.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace Operations.Cashiers;

/// <summary>
/// Domain service for the cash-drawer session aggregate. Owns the cross-aggregate
/// invariant that a cashier may have only ONE Open shift at a time (which needs a
/// repository query, so it cannot live in the entity constructor).
/// </summary>
public class CashierShiftManager : DomainService
{
    private readonly ICashierShiftRepository _shiftRepository;

    public CashierShiftManager(ICashierShiftRepository shiftRepository)
    {
        _shiftRepository = shiftRepository;
    }

    public async Task<AppCashierShift> CreateOpenAsync(
        Guid branchId,
        Guid cashierUserId,
        decimal openingFloat)
    {
        if (await _shiftRepository.HasOpenShiftAsync(cashierUserId))
        {
            throw new BusinessException(OperationsErrorCodes.ShiftAlreadyOpen)
                .WithData("CashierUserId", cashierUserId);
        }

        return new AppCashierShift(
            GuidGenerator.Create(),
            branchId,
            cashierUserId,
            Clock.Now,
            openingFloat);
    }
}
