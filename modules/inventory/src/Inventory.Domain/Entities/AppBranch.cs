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

    /// <summary>
    /// What this branch is for: <see cref="BranchTypes.SalesBranch"/> (sells finished goods)
    /// or <see cref="BranchTypes.MainKitchen"/> (produces goods from raw materials).
    /// </summary>
    public string BranchType { get; private set; } = BranchTypes.SalesBranch;

    protected AppBranch() { }

    internal AppBranch(
        Guid id,
        string name,
        string? address = null,
        string? phone = null,
        string? email = null,
        Guid? managerUserId = null,
        bool isActive = true,
        string branchType = BranchTypes.SalesBranch)
        : base(id)
    {
        SetName(name);
        Address = address;
        Phone = phone;
        Email = email;
        ManagerUserId = managerUserId;
        IsActive = isActive;
        SetBranchType(branchType);
    }

    public void UpdateInfo(
        string? address,
        string? phone,
        string? email,
        Guid? managerUserId,
        bool isActive,
        string branchType = BranchTypes.SalesBranch)
    {
        Address = address;
        Phone = phone;
        Email = email;
        ManagerUserId = managerUserId;
        IsActive = isActive;
        SetBranchType(branchType);
    }

    public void SetBranchType(string branchType)
    {
        if (!BranchTypes.IsValid(branchType))
        {
            throw new BusinessException(InventoryErrorCodes.InvalidBranchType)
                .WithData("BranchType", branchType);
        }

        BranchType = branchType;
    }

    internal void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim();
    }
}
