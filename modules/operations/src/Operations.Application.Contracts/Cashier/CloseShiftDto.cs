using System;
using System.ComponentModel.DataAnnotations;

namespace Operations.Cashier;

public class CloseShiftDto
{
    public Guid ShiftId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CountedCash { get; set; }
}
