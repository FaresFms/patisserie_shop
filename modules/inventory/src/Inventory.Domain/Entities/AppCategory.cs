using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

public class AppCategory : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; internal set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    protected AppCategory() { }

    internal AppCategory(Guid id, string name, string? description = null, bool isActive = true)
        : base(id)
    {
        SetName(name);
        Description = description;
        IsActive = isActive;
    }

    public void UpdateInfo(string? description, bool isActive)
    {
        Description = description;
        IsActive = isActive;
    }

    internal void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim();
    }
}
