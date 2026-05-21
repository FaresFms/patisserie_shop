using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

public class AppCategory : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    protected AppCategory() { }

    public AppCategory(Guid id, string name, string? description = null, bool isActive = true)
        : base(id)
    {
        Name = name;
        Description = description;
        IsActive = isActive;
    }
}
