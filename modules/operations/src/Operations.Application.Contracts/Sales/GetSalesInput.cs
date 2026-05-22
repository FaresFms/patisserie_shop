using System;
using Volo.Abp.Application.Dtos;

namespace Operations.Sales;

public class GetSalesInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? BranchId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
