using System;
using Volo.Abp.Application.Dtos;

namespace Production.Formulas;

public class GetProductionFormulasInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? FinishedProductId { get; set; }
    public bool? IsActive { get; set; }
}
