using System;
using Volo.Abp.Application.Dtos;

namespace Production.BranchRequests;

public class GetBranchProductionRequestsInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public string? Status { get; set; }
    public Guid? BranchId { get; set; }
}
