using System;

namespace Intelligence.Events;

[Serializable]
public class DecisionMadeEto
{
    public Guid DecisionLogId { get; set; }
    public string DecisionType { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Guid? BranchId { get; set; }
}
