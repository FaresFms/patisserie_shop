using System;

namespace Inventory.Branches;

public class BranchLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}
