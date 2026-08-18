using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Production.Entities;
using Volo.Abp.Domain.Repositories;

namespace Production.Formulas;

/// <summary>
/// Custom repository for the <see cref="AppProductionFormula"/> aggregate. Owns the
/// items-eager load, the filtered/paged list (joined to the Inventory product name),
/// the default-formula lookup, and the cost-side helpers (ingredient cost prices +
/// the producible / ingredient product dropdowns) so neither the app service nor a
/// domain service ever touches an Inventory queryable.
/// FROZEN CONTRACT — later waves depend on this interface.
/// </summary>
public interface IProductionFormulaRepository : IRepository<AppProductionFormula, Guid>
{
    /// <summary>Loads a formula with its ingredient items eagerly populated.</summary>
    Task<AppProductionFormula> GetWithItemsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<long> CountFilteredAsync(
        string? filter,
        Guid? finishedProductId,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<List<ProductionFormulaListItem>> GetFilteredListAsync(
        string? filter,
        Guid? finishedProductId,
        bool? isActive,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    /// <summary>The single active default formula for a finished product, or null.</summary>
    Task<AppProductionFormula?> GetActiveDefaultForProductAsync(
        Guid finishedProductId,
        CancellationToken cancellationToken = default);

    Task<int> GetNextVersionAsync(
        Guid finishedProductId,
        CancellationToken cancellationToken = default);

    Task<bool> VersionExistsAsync(
        Guid finishedProductId,
        int version,
        Guid? excludingFormulaId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Per-ingredient unit cost (the product's CostPrice) for the supplied ids. Missing
    /// ids simply don't appear; the caller treats them as zero cost.
    /// </summary>
    Task<Dictionary<Guid, decimal>> GetIngredientCostPricesAsync(
        IReadOnlyCollection<Guid> ingredientProductIds,
        CancellationToken cancellationToken = default);

    /// <summary>Active producible finished products for the formula's product dropdown.</summary>
    Task<List<ProductionProductLookup>> GetProducibleFinishedProductsLookupAsync(
        string? filter,
        int maxResults,
        CancellationToken cancellationToken = default);

    /// <summary>Active ingredient products (RawMaterial / Packaging / SemiFinished) for the items dropdown.</summary>
    Task<List<ProductionProductLookup>> GetIngredientProductsLookupAsync(
        string? filter,
        int maxResults,
        CancellationToken cancellationToken = default);
}
