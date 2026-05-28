using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

public class AppBranch : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; internal set; } = null!;
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public Guid? ManagerUserId { get; private set; }
    public bool IsActive { get; private set; } = true;

    protected AppBranch() { }

    internal AppBranch(
        Guid id,
        string name,
        string? address = null,
        string? phone = null,
        string? email = null,
        Guid? managerUserId = null,
        bool isActive = true)
        : base(id)
    {
        SetName(name);
        Address = address;
        Phone = phone;
        Email = email;
        ManagerUserId = managerUserId;
        IsActive = isActive;
    }

    public void UpdateInfo(
        string? address,
        string? phone,
        string? email,
        Guid? managerUserId,
        bool isActive)
    {
        Address = address;
        Phone = phone;
        Email = email;
        ManagerUserId = managerUserId;
        IsActive = isActive;
    }

    internal void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim();
    }
}
