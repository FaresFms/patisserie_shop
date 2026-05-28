using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

public class AppSupplier : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; internal set; } = null!;
    public string? ContactPerson { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;

    protected AppSupplier() { }

    internal AppSupplier(
        Guid id,
        string name,
        string? contactPerson = null,
        string? phone = null,
        string? email = null,
        string? address = null,
        bool isActive = true)
        : base(id)
    {
        SetName(name);
        ContactPerson = contactPerson;
        Phone = phone;
        Email = email;
        Address = address;
        IsActive = isActive;
    }

    public void UpdateInfo(
        string? contactPerson,
        string? phone,
        string? email,
        string? address,
        bool isActive)
    {
        ContactPerson = contactPerson;
        Phone = phone;
        Email = email;
        Address = address;
        IsActive = isActive;
    }

    internal void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim();
    }
}
