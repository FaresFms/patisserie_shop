using System;

namespace Operations.Events;

[Serializable]
public class CashierShiftVarianceEto
{
    public Guid ShiftId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CashierUserId { get; set; }
    public DateTime ClosedAt { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal CountedCash { get; set; }
    public decimal Variance { get; set; }
}
