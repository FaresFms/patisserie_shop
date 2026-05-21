using System;

namespace Inventory.Branches;

public class UpdateBranchDto
{
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Guid? ManagerUserId { get; set; }
    public bool IsActive { get; set; } = true;
}
