using System;
using System.Collections.Generic;
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

    /// <summary>Match any of these statuses (e.g. the "Handled" tab = Acknowledged/Dismissed/Executed). Combines with <see cref="Status"/>.</summary>
    public List<string>? Statuses { get; set; }

    /// <summary>Match any of these decision types (e.g. a category group). Combines with <see cref="DecisionType"/>.</summary>
    public List<string>? DecisionTypes { get; set; }
}
