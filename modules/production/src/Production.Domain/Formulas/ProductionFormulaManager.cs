using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.Entities;
using Production.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Production.Formulas;

/// <summary>
/// Domain service for the <see cref="AppProductionFormula"/> aggregate. The entity
/// constructor is internal, so a formula is only ever created through here. This
/// service owns the cross-aggregate invariants that need repository queries:
///   • the finished product must exist and be IsProducible,
///   • every ingredient must exist and be a RawMaterial / Packaging / SemiFinished
///     product (never a FinishedGood),
///   • at most one active default formula may exist per finished product.
/// </summary>
public class ProductionFormulaManager : DomainService
{
    private readonly IRepository<AppProductionFormula, Guid> _formulaRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;

    public ProductionFormulaManager(
        IRepository<AppProductionFormula, Guid> formulaRepository,
        IRepository<AppProduct, Guid> productRepository)
    {
        _formulaRepository = formulaRepository;
        _productRepository = productRepository;
    }

    public async Task<AppProductionFormula> CreateAsync(
        Guid finishedProductId,
        string formulaName,
        int outputQuantity,
        int version,
        decimal expectedWastePercent,
        decimal laborCostPerBatch,
        decimal overheadCostPerBatch,
        int estimatedProductionMinutes,
        bool isActive,
        bool isDefault,
        string? notes)
    {
        await EnsureFinishedProductIsProducibleAsync(finishedProductId);

        var formula = new AppProductionFormula(
            GuidGenerator.Create(),
            finishedProductId,
            formulaName,
            outputQuantity,
            version <= 0 ? 1 : version,
            expectedWastePercent,
            laborCostPerBatch,
            overheadCostPerBatch,
            estimatedProductionMinutes,
            isActive,
            isDefault: false,
            notes);

        if (isDefault)
        {
            await MarkDefaultAsync(formula);
        }

        return formula;
    }

    /// <summary>
    /// Builds an in-memory, NON-persisted formula (with its items) for the planned-cost
    /// preview. No DB writes, no default-flag side effects — the entity invariants still
    /// run via the constructor / AddItem. Used by the editor's live cost preview.
    /// </summary>
    public AppProductionFormula BuildTransient(
        Guid finishedProductId,
        string formulaName,
        int outputQuantity,
        decimal expectedWastePercent,
        decimal laborCostPerBatch,
        decimal overheadCostPerBatch,
        int estimatedProductionMinutes,
        IEnumerable<(Guid IngredientProductId, int Quantity, decimal LossPercent, int SortOrder)> items)
    {
        var formula = new AppProductionFormula(
            GuidGenerator.Create(),
            finishedProductId,
            formulaName,
            outputQuantity,
            version: 1,
            expectedWastePercent,
            laborCostPerBatch,
            overheadCostPerBatch,
            estimatedProductionMinutes);

        foreach (var item in items)
        {
            formula.AddItem(
                GuidGenerator.Create(),
                item.IngredientProductId,
                item.Quantity,
                item.LossPercent,
                item.SortOrder);
        }

        return formula;
    }

    /// <summary>
    /// Ensures the finished product exists and is producible. Throws if missing
    /// (GetAsync) or not producible.
    /// </summary>
    public async Task EnsureFinishedProductIsProducibleAsync(Guid finishedProductId)
    {
        var product = await _productRepository.GetAsync(finishedProductId);
        if (!product.IsProducible)
        {
            throw new BusinessException(ProductionErrorCodes.FinishedProductNotProducible)
                .WithData("FinishedProductId", finishedProductId);
        }
    }

    /// <summary>
    /// Ensures every supplied ingredient exists and is a RawMaterial / Packaging /
    /// SemiFinished product. A FinishedGood (or any unknown type) is rejected.
    /// </summary>
    public async Task EnsureIngredientsAreValidAsync(IEnumerable<Guid> ingredientProductIds)
    {
        var ids = ingredientProductIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var products = await _productRepository.GetListAsync(p => ids.Contains(p.Id));
        var byId = products.ToDictionary(p => p.Id);

        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var product))
            {
                // Surfaces as the standard ABP "entity not found" for the missing ingredient.
                await _productRepository.GetAsync(id);
                continue;
            }

            if (!IsValidIngredientType(product.ProductType))
            {
                throw new BusinessException(ProductionErrorCodes.InvalidIngredientProductType)
                    .WithData("IngredientProductId", id)
                    .WithData("ProductType", product.ProductType);
            }
        }
    }

    /// <summary>
    /// Marks <paramref name="formula"/> as the default for its finished product,
    /// unmarking any other formula currently flagged default for the same product.
    /// </summary>
    public async Task MarkDefaultAsync(AppProductionFormula formula)
    {
        Check.NotNull(formula, nameof(formula));

        var others = await _formulaRepository.GetListAsync(f =>
            f.FinishedProductId == formula.FinishedProductId
            && f.Id != formula.Id
            && f.IsDefault);

        foreach (var other in others)
        {
            other.UnmarkDefault();
            await _formulaRepository.UpdateAsync(other);
        }

        formula.MarkDefault();
    }

    private static bool IsValidIngredientType(string productType) =>
        productType == ProductTypes.RawMaterial
        || productType == ProductTypes.Packaging
        || productType == ProductTypes.SemiFinished;
}
