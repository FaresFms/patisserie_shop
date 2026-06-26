using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Production.Plans;

public class ProductionPlanDto : EntityDto<Guid>
{
    public string PlanNumber { get; set; } = null!;
    public Guid KitchenBranchId { get; set; }
    public string? KitchenBranchName { get; set; }
    public DateTime ProductionDate { get; set; }
    public string Status { get; set; } = ProductionPlanStatuses.Draft;
    public Guid? CreatedByUserId { get; set; }
    public Guid? ConfirmedByUserId { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? Notes { get; set; }
    public List<ProductionPlanLineDto> Lines { get; set; } = new();
}
