using System;
using Volo.Abp.Application.Dtos;

namespace Intelligence.Decisions;

public class GetDecisionLogsInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public string? DecisionType { get; set; }
    public string? Status { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? ProductId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
