using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Inventory.Entities;

public class AppSupplier : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; set; } = null!;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    protected AppSupplier() { }

    public AppSupplier(
        Guid id,
        string name,
        string? contactPerson = null,
        string? phone = null,
        string? email = null,
        string? address = null,
        bool isActive = true)
        : base(id)
    {
        Name = name;
        ContactPerson = contactPerson;
        Phone = phone;
        Email = email;
        Address = address;
        IsActive = isActive;
    }
}
