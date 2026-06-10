using System;
using System.Collections.Generic;
using System.Linq;
using Intelligence.Entities;

namespace Intelligence.Decisions;

/// <summary>
/// Shared scope-matrix resolver used by the background scanners (and mirroring the
/// precedence the real-time DecisionMakerService relies on). Given a list of rules
/// already sorted by Priority descending, it returns the single best-fit rule for a
/// (product, branch) pair using scope specificity:
///
///   1. exact      — this product in this branch
///   2. product    — this product in all branches  (BranchId == null)
///   3. branch     — all products in this branch    (ProductId == null)
///   4. global     — all products in all branches   (both null)
///
/// Within a scope class the first rule wins, which is the highest Priority because the
/// caller pre-sorts by Priority descending.
/// </summary>
internal static class RuleScopeMatcher
{
    public static AppInventoryRule? MatchBestRule(
        IReadOnlyList<AppInventoryRule> rulesByPriorityDesc,
        Guid productId,
        Guid branchId)
    {
        // 1) Exact: this product in this branch.
        var rule = rulesByPriorityDesc.FirstOrDefault(r =>
            r.ProductId == productId && r.BranchId == branchId);
        if (rule != null) return rule;

        // 2) Product-only: this product across all branches.
        rule = rulesByPriorityDesc.FirstOrDefault(r =>
            r.ProductId == productId && r.BranchId == null);
        if (rule != null) return rule;

        // 3) Branch-only: all products in this branch.
        rule = rulesByPriorityDesc.FirstOrDefault(r =>
            r.ProductId == null && r.BranchId == branchId);
        if (rule != null) return rule;

        // 4) Global: all products in all branches.
        return rulesByPriorityDesc.FirstOrDefault(r =>
            r.ProductId == null && r.BranchId == null);
    }
}
