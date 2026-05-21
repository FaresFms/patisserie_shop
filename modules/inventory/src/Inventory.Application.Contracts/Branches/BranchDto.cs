using System;
using Volo.Abp.Application.Dtos;

namespace Inventory.Branches;

public class BranchDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Guid? ManagerUserId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreationTime { get; set; }
}
