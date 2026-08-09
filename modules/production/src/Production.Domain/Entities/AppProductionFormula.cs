using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Data;
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
    private const string ApprovalStatusProperty = "Production.Formula.ApprovalStatus";
    private const string ApprovedAtProperty = "Production.Formula.ApprovedAt";
    private const string ApprovedByProperty = "Production.Formula.ApprovedByUserId";
    private const string IngredientAllergensProperty = "Production.Formula.IngredientAllergens";
    private const string WorkCenterCodeProperty = "Production.Formula.WorkCenterCode";
    private const string PreparationStepsProperty = "Production.Formula.PreparationSteps";

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

    /// <summary>
    /// Legacy default formulas are treated as approved so existing kitchens keep
    /// working after the revision workflow is introduced.
    /// </summary>
    public string ApprovalStatus => this.GetProperty<string>(ApprovalStatusProperty)
        ?? (IsDefault ? ProductionFormulaStatuses.Approved : ProductionFormulaStatuses.Draft);
    public DateTime? ApprovedAt => this.GetProperty<DateTime?>(ApprovedAtProperty);
    public Guid? ApprovedByUserId => this.GetProperty<Guid?>(ApprovedByProperty);
    public string? WorkCenterCode => this.GetProperty<string>(WorkCenterCodeProperty);
    public string? PreparationSteps => this.GetProperty<string>(PreparationStepsProperty);
    public bool IsEditable => ApprovalStatus == ProductionFormulaStatuses.Draft;

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
        EnsureEditable();
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
        EnsureEditable();
        if (string.IsNullOrWhiteSpace(formulaName))
        {
            throw new BusinessException(ProductionErrorCodes.FormulaNameRequired);
        }
        FormulaName = formulaName.Trim();
    }

    public void SetOutputQuantity(int outputQuantity)
    {
        EnsureEditable();
        if (outputQuantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOutputQuantity)
                .WithData("OutputQuantity", outputQuantity);
        }
        OutputQuantity = outputQuantity;
    }

    public void SetVersion(int version)
    {
        EnsureEditable();
        if (version <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidVersion)
                .WithData("Version", version);
        }
        Version = version;
    }

    public void SetExpectedWastePercent(decimal expectedWastePercent)
    {
        EnsureEditable();
        if (expectedWastePercent < 0m || expectedWastePercent >= 100m)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidWastePercent)
                .WithData("ExpectedWastePercent", expectedWastePercent);
        }
        ExpectedWastePercent = expectedWastePercent;
    }

    public void SetLaborCostPerBatch(decimal laborCostPerBatch)
    {
        EnsureEditable();
        if (laborCostPerBatch < 0m)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidCost)
                .WithData("LaborCostPerBatch", laborCostPerBatch);
        }
        LaborCostPerBatch = laborCostPerBatch;
    }

    public void SetOverheadCostPerBatch(decimal overheadCostPerBatch)
    {
        EnsureEditable();
        if (overheadCostPerBatch < 0m)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidCost)
                .WithData("OverheadCostPerBatch", overheadCostPerBatch);
        }
        OverheadCostPerBatch = overheadCostPerBatch;
    }

    public void SetEstimatedProductionMinutes(int estimatedProductionMinutes)
    {
        EnsureEditable();
        if (estimatedProductionMinutes < 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidProductionMinutes)
                .WithData("EstimatedProductionMinutes", estimatedProductionMinutes);
        }
        EstimatedProductionMinutes = estimatedProductionMinutes;
    }

    public void SetNotes(string? notes)
    {
        EnsureEditable();
        Notes = notes;
    }

    public void SetPhase3Details(
        string? workCenterCode,
        string? preparationSteps,
        IReadOnlyDictionary<Guid, string>? ingredientAllergens)
    {
        EnsureEditable();
        ExtraProperties[WorkCenterCodeProperty] = NormalizeOptional(workCenterCode);
        ExtraProperties[PreparationStepsProperty] = NormalizeOptional(preparationSteps);

        var normalized = ingredientAllergens?
            .Where(x => _items.Any(item => item.IngredientProductId == x.Key))
            .ToDictionary(x => x.Key, x => NormalizeAllergens(x.Value));
        ExtraProperties[IngredientAllergensProperty] = JsonSerializer.Serialize(
            normalized ?? new Dictionary<Guid, string>());
    }

    public IReadOnlyDictionary<Guid, string> GetIngredientAllergens()
    {
        var json = this.GetProperty<string>(IngredientAllergensProperty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<Guid, string>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<Guid, string>>(json)
                ?? new Dictionary<Guid, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<Guid, string>();
        }
    }

    // ── Items (owned children) ──

    public AppProductionFormulaItem AddItem(
        Guid itemId,
        Guid ingredientProductId,
        int quantity,
        decimal lossPercent,
        int sortOrder)
    {
        EnsureEditable();
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
        EnsureEditable();
        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new BusinessException(ProductionErrorCodes.FormulaItemNotFound)
                .WithData("ItemId", itemId);
        _items.Remove(item);
    }

    public void ClearItems()
    {
        EnsureEditable();
        _items.Clear();
    }

    public void EnsureHasIngredients()
    {
        if (_items.Count == 0)
        {
            throw new BusinessException(ProductionErrorCodes.FormulaIngredientsRequired);
        }
    }

    // ── State flags ──

    public void Activate()
    {
        EnsureEditable();
        IsActive = true;
    }

    public void Deactivate()
    {
        EnsureEditable();
        IsActive = false;
    }

    public void MarkDefault()
    {
        if (ApprovalStatus != ProductionFormulaStatuses.Approved)
        {
            throw new BusinessException(ProductionErrorCodes.FormulaMustBeApproved);
        }
        IsDefault = true;
        IsActive = true;
    }

    public void UnmarkDefault() => IsDefault = false;

    public void Approve(Guid? approvedByUserId, DateTime approvedAt)
    {
        EnsureEditable();
        EnsureHasIngredients();
        ExtraProperties[ApprovalStatusProperty] = ProductionFormulaStatuses.Approved;
        ExtraProperties[ApprovedAtProperty] = approvedAt;
        ExtraProperties[ApprovedByProperty] = approvedByUserId;
        IsActive = true;
    }

    public void Retire()
    {
        if (ApprovalStatus != ProductionFormulaStatuses.Approved)
        {
            throw new BusinessException(ProductionErrorCodes.FormulaMustBeApproved);
        }
        ExtraProperties[ApprovalStatusProperty] = ProductionFormulaStatuses.Retired;
        IsDefault = false;
        IsActive = false;
    }

    private void EnsureEditable()
    {
        if (ApprovalStatus != ProductionFormulaStatuses.Draft)
        {
            throw new BusinessException(ProductionErrorCodes.ApprovedFormulaIsImmutable)
                .WithData("FormulaId", Id)
                .WithData("Status", ApprovalStatus);
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeAllergens(string? value) =>
        string.Join(", ", (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase));
}
