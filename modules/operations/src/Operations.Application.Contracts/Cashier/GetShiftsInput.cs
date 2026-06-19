using System;
using Volo.Abp.Application.Dtos;

namespace Operations.Cashier;

/// <summary>Manager drawer view filter: optional branch, open-only toggle, paging.</summary>
public class GetShiftsInput : PagedAndSortedResultRequestDto
{
    public Guid? BranchId { get; set; }
    public bool OpenOnly { get; set; }
}
