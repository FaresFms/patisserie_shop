using System;
using System.Collections.Generic;

namespace patisserie_shop.Decisions;

/// <summary>
/// Non-persisting preview of the corrective action a decision execute will create.
/// </summary>
public class DecisionActionPreviewDto
{
    public bool ActionCreated { get; set; }

    /// <summary>One of the Intelligence DecisionActionTypes constants, or null.</summary>
    public string? ActionType { get; set; }

    public string? ProductName { get; set; }
    public string? BranchName { get; set; }
    public string? SupplierName { get; set; }
    public string? SourceBranchName { get; set; }
    public string? TargetBranchName { get; set; }
    public int? Quantity { get; set; }
    public string? Notes { get; set; }

    // Ids so the UI can open the real creation form pre-filled instead of
    // silently creating the document server-side.
    public Guid? ProductId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? SourceBranchId { get; set; }
    public Guid? TargetBranchId { get; set; }
    public decimal? UnitPrice { get; set; }

    public List<DecisionActionPreviewLineDto> Lines { get; set; } = new();
}

public class DecisionActionPreviewLineDto
{
    public string? ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Notes { get; set; }
}
