using System;
using System.Collections.Generic;

namespace patisserie_shop.Dashboard;

/// <summary>
/// One explainable 0–100 composite health score for a single branch, with the
/// per-component breakdown that produced it. Every component carries a human
/// <see cref="HealthComponentDto.Detail"/> string so the score is fully traceable
/// (thesis requirement: deterministic, no AI/ML).
/// </summary>
public class BranchHealthDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;

    /// <summary>Sum of all component points, 0–100.</summary>
    public int Score { get; set; }

    /// <summary>Letter grade derived from <see cref="Score"/>: A 85+, B 70+, C 55+, else D.</summary>
    public string Grade { get; set; } = string.Empty;

    public List<HealthComponentDto> Components { get; set; } = new();
}

/// <summary>
/// A single weighted contribution to a branch's health score. Points are out of
/// MaxPoints; the weights of all components sum to 100.
/// </summary>
public class HealthComponentDto
{
    /// <summary>Stable component key (e.g. "StockHealth") — the UI localizes it.</summary>
    public string Name { get; set; } = string.Empty;

    public int Points { get; set; }
    public int MaxPoints { get; set; }

    /// <summary>Human-readable explanation of how Points was derived.</summary>
    public string Detail { get; set; } = string.Empty;
}
