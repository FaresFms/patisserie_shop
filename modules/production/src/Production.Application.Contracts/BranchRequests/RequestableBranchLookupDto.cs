using System;

namespace Production.BranchRequests;

public class RequestableBranchLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}
