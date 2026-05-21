using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

public class AppBranch : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Guid? ManagerUserId { get; set; }
    public bool IsActive { get; set; } = true;

    protected AppBranch() { }

    public AppBranch(
        Guid id,
        string name,
        string? address = null,
        string? phone = null,
        string? email = null,
        Guid? managerUserId = null,
        bool isActive = true)
        : base(id)
    {
        Name = name;
        Address = address;
        Phone = phone;
        Email = email;
        ManagerUserId = managerUserId;
        IsActive = isActive;
    }
}
