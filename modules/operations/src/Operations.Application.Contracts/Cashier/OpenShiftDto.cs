using System;
using System.ComponentModel.DataAnnotations;

namespace Operations.Cashier;

public class OpenShiftDto
{
    public Guid BranchId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal OpeningFloat { get; set; }
}
