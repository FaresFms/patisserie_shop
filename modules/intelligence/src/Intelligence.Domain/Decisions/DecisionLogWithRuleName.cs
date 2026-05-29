using Intelligence.Entities;

namespace Intelligence.Decisions;

/// <summary>
/// Typed read-model returned by IDecisionLogRepository when the dashboard needs
/// the originating rule's name alongside the immutable decision log.
/// Living next to the repository interface (Domain) per CLAUDE.md: no anonymous
/// types or tuples crossing the repository boundary.
/// </summary>
public class DecisionLogWithRuleName
{
    public AppDecisionLog DecisionLog { get; init; } = null!;
    public string? RuleName { get; init; }
}
