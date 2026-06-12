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

    /// <summary>Average days between placing a purchase order and receiving it.</summary>
    public int LeadTimeDays { get; private set; } = 3;

    protected AppSupplier() { }

    internal AppSupplier(
        Guid id,
        string name,
        string? contactPerson = null,
        string? phone = null,
        string? email = null,
        string? address = null,
        bool isActive = true,
        int leadTimeDays = 3)
        : base(id)
    {
        SetName(name);
        ContactPerson = contactPerson;
        Phone = phone;
        Email = email;
        Address = address;
        IsActive = isActive;
        SetLeadTimeDays(leadTimeDays);
    }

    public void UpdateInfo(
        string? contactPerson,
        string? phone,
        string? email,
        string? address,
        bool isActive,
        int leadTimeDays)
    {
        ContactPerson = contactPerson;
        Phone = phone;
        Email = email;
        Address = address;
        IsActive = isActive;
        SetLeadTimeDays(leadTimeDays);
    }

    internal void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim();
    }

    private void SetLeadTimeDays(int leadTimeDays)
    {
        if (leadTimeDays < 0 || leadTimeDays > 365)
        {
            throw new BusinessException(InventoryErrorCodes.InvalidSupplierLeadTime)
                .WithData("LeadTimeDays", leadTimeDays);
        }

        LeadTimeDays = leadTimeDays;
    }
}
