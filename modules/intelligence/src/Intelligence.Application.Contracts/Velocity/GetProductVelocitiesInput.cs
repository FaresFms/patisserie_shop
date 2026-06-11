using System;
using Volo.Abp.Application.Dtos;

namespace Intelligence.Velocity;

public class GetProductVelocitiesInput : PagedAndSortedResultRequestDto
{
    /// <summary>Matches product name or SKU (case-insensitive contains).</summary>
    public string? Filter { get; set; }

    public Guid? BranchId { get; set; }
}
