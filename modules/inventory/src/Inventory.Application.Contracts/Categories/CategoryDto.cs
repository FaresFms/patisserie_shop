using System;
using Volo.Abp.Application.Dtos;

namespace Inventory.Categories;

public class CategoryDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreationTime { get; set; }
}
