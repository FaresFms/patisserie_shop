using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Production.Entities;

/// <summary>
/// A recipe / bill-of-materials for a producible finished product. The aggregate owns
/// its ingredient items (children, no repository) and enforces its own invariants
/// through behaviour methods. Cross-aggregate invariants (the finished product must be
/// producible, ingredients must be raw/packaging/semi, only one default per product)
/// live in <see cref="Production.Formulas.ProductionFormulaManager"/>.
/// </summary>
public class AppProductionFormula : FullAuditedAggregateRoot<Guid>
{
    public Guid FinishedProductId { get; private set; }
    public string FormulaName { get; private set; } = null!;
    public int Version { get; private set; } = 1;

    /// <summary>How many finished units one full batch of this formula yields (in the product's Unit). Always &gt; 0.</summary>
    public int OutputQuantity { get; private set; }

    /// <summary>Expected batch-level waste percentage of the finished output, 0–100.</summary>
    public decimal ExpectedWastePercent { get; private set; }

    public decimal LaborCostPerBatch { get; private set; }
    public decimal OverheadCostPerBatch { get; private set; }
    public int EstimatedProductionMinutes { get; private set; }

    public bool IsActive { get; private set; } = true;
    public bool IsDefault { get; private set; }
    public string? Notes { get; private set; }

    private readonly List<AppProductionFormulaItem> _items = new();
    public IReadOnlyCollection<AppProductionFormulaItem> Items => new ReadOnlyCollection<AppProductionFormulaItem>(_items);

    protected AppProductionFormula() { }

    internal AppProductionFormula(
        Guid id,
        Guid finishedProductId,
        string formulaName,
        int outputQuantity,
        int version = 1,
        decimal expectedWastePercent = 0m,
        decimal laborCostPerBatch = 0m,
        decimal overheadCostPerBatch = 0m,
        int estimatedProductionMinutes = 0,
        bool isActive = true,
        bool isDefault = false,
        string? notes = null)
        : base(id)
    {
        FinishedProductId = finishedProductId;
        SetVersion(version);
        UpdateInfo(
            formulaName,
            outputQuantity,
            expectedWastePercent,
            laborCostPerBatch,
            overheadCostPerBatch,
            estimatedProductionMinutes,
            notes);
        IsActive = isActive;
        IsDefault = isDefault;
    }

    public void UpdateInfo(
        string formulaName,
        int outputQuantity,
        decimal expectedWastePercent,
        decimal laborCostPerBatch,
        decimal overheadCostPerBatch,
        int estimatedProductionMinutes,
        string? notes)
    {
        SetFormulaName(formulaName);
        SetOutputQuantity(outputQuantity);
        SetExpectedWastePercent(expectedWastePercent);
        SetLaborCostPerBatch(laborCostPerBatch);
        SetOverheadCostPerBatch(overheadCostPerBatch);
        SetEstimatedProductionMinutes(estimatedProductionMinutes);
        Notes = notes;
    }

    // ── Field setters (validate invariants) ──

    public void SetFormulaName(string formulaName)
    {
        if (string.IsNullOrWhiteSpace(formulaName))
        {
            throw new BusinessException(ProductionErrorCodes.FormulaNameRequired);
        }
        FormulaName = formulaName.Trim();
    }

    public void SetOutputQuantity(int outputQuantity)
    {
        if (outputQuantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOutputQuantity)
                .WithData("OutputQuantity", outputQuantity);
        }
        OutputQuantity = outputQuantity;
    }

    public void SetVersion(int version)
    {
        if (version <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidVersion)
                .WithData("Version", version);
        }
        Version = version;
    }

    public void SetExpectedWastePercent(decimal expectedWastePercent)
    {
        if (expectedWastePercent < 0m || expectedWastePercent > 100m)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidWastePercent)
                .WithData("ExpectedWastePercent", expectedWastePercent);
        }
        ExpectedWastePercent = expectedWastePercent;
    }

    public void SetLaborCostPerBatch(decimal laborCostPerBatch)
    {
        if (laborCostPerBatch < 0m)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidCost)
                .WithData("LaborCostPerBatch", laborCostPerBatch);
        }
        LaborCostPerBatch = laborCostPerBatch;
    }

    public void SetOverheadCostPerBatch(decimal overheadCostPerBatch)
    {
        if (overheadCostPerBatch < 0m)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidCost)
                .WithData("OverheadCostPerBatch", overheadCostPerBatch);
        }
        OverheadCostPerBatch = overheadCostPerBatch;
    }

    public void SetEstimatedProductionMinutes(int estimatedProductionMinutes)
    {
        if (estimatedProductionMinutes < 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidProductionMinutes)
                .WithData("EstimatedProductionMinutes", estimatedProductionMinutes);
        }
        EstimatedProductionMinutes = estimatedProductionMinutes;
    }

    public void SetNotes(string? notes) => Notes = notes;

    // ── Items (owned children) ──

    public AppProductionFormulaItem AddItem(
        Guid itemId,
        Guid ingredientProductId,
        int quantity,
        decimal lossPercent,
        int sortOrder)
    {
        if (_items.Any(i => i.IngredientProductId == ingredientProductId))
        {
            throw new BusinessException(ProductionErrorCodes.DuplicateIngredient)
                .WithData("IngredientProductId", ingredientProductId);
        }

        var item = new AppProductionFormulaItem(itemId, Id, ingredientProductId, quantity, lossPercent, sortOrder);
        _items.Add(item);
        return item;
    }

    public void RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new BusinessException(ProductionErrorCodes.FormulaItemNotFound)
                .WithData("ItemId", itemId);
        _items.Remove(item);
    }

    public void ClearItems() => _items.Clear();

    // ── State flags ──

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void MarkDefault() => IsDefault = true;

    public void UnmarkDefault() => IsDefault = false;
}
