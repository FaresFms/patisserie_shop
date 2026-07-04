using System;

namespace Inventory.Branches;

public class BranchLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    /// <summary>One of <c>BranchTypes</c> — lets pickers suggest the Main Kitchen as the default transfer source.</summary>
    public string? BranchType { get; set; }
}
