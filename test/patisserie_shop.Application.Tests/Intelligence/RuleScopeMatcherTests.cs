using System;
using System.Collections.Generic;
using System.Linq;
using Intelligence.Decisions;
using Intelligence.Entities;
using Intelligence.Rules;
using Shouldly;
using Xunit;

namespace patisserie_shop.Intelligence;

/// <summary>
/// Pure unit tests for the scope matrix the background scanners use to pick the
/// single governing rule for a (product, branch) pair:
/// exact &gt; product-wide &gt; branch-wide &gt; global, and within one scope class the
/// highest Priority wins (the caller pre-sorts by Priority descending).
/// Internal types are reached via InternalsVisibleTo.
/// </summary>
public class RuleScopeMatcherTests
{
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();

    private static AppInventoryRule Rule(
        string name, Guid? productId, Guid? branchId, int priority = 0)
        => new(
            Guid.NewGuid(),
            ruleName: name,
            ruleType: InventoryRuleTypes.LowStock,
            productId: productId,
            branchId: branchId,
            thresholdValue: 5,
            priority: priority);

    /// <summary>Mirrors the scanners: rules are handed over sorted by Priority descending.</summary>
    private static AppInventoryRule? Match(params AppInventoryRule[] rules)
        => RuleScopeMatcher.MatchBestRule(
            rules.OrderByDescending(r => r.Priority).ToList(), ProductId, BranchId);

    [Fact]
    public void Exact_Scope_Wins_Even_Against_Higher_Priority_Broader_Rules()
    {
        var exact = Rule("exact", ProductId, BranchId, priority: 0);
        var globalHighPriority = Rule("global", null, null, priority: 100);
        var productHighPriority = Rule("product", ProductId, null, priority: 100);

        var winner = Match(exact, globalHighPriority, productHighPriority);

        // Scope specificity is evaluated BEFORE priority: a low-priority exact rule
        // still beats high-priority broader rules.
        winner.ShouldBe(exact);
    }

    [Fact]
    public void Product_Scope_Beats_Branch_Scope_And_Global()
    {
        var productWide = Rule("product", ProductId, null);
        var branchWide = Rule("branch", null, BranchId, priority: 50);
        var global = Rule("global", null, null, priority: 50);

        Match(productWide, branchWide, global).ShouldBe(productWide);
    }

    [Fact]
    public void Branch_Scope_Beats_Global()
    {
        var branchWide = Rule("branch", null, BranchId);
        var global = Rule("global", null, null, priority: 50);

        Match(branchWide, global).ShouldBe(branchWide);
    }

    [Fact]
    public void Global_Is_The_Last_Resort_And_Foreign_Scopes_Never_Match()
    {
        var global = Rule("global", null, null);
        var otherProduct = Rule("other-product", Guid.NewGuid(), null, priority: 99);
        var otherBranch = Rule("other-branch", null, Guid.NewGuid(), priority: 99);

        Match(global, otherProduct, otherBranch).ShouldBe(global);

        // No global and nothing matching → null (no governing rule).
        Match(otherProduct, otherBranch).ShouldBeNull();
    }

    [Fact]
    public void Highest_Priority_Wins_Within_The_Same_Scope_Class()
    {
        var critical = Rule("critical", null, null, priority: 10);
        var standard = Rule("standard", null, null, priority: 0);

        Match(standard, critical).ShouldBe(critical);
    }
}
