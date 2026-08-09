using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inventory;
using Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Production.Entities;
using Production.Formulas;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Production.EntityFrameworkCore;

/// <summary>
/// Implements <see cref="IProductionFormulaRepository"/>. Production formulas live in
/// the Production DbContext while products live in the Inventory DbContext, so the
/// product name / cost overlay is done in memory by injecting the Inventory product
/// repository (the same cross-module pattern the Intelligence velocity repo uses).
/// Filtering, sort-key translation and paging all live here so the app service stays
/// LINQ-free.
/// </summary>
public class ProductionFormulaRepository
    : EfCoreRepository<ProductionDbContext, AppProductionFormula, Guid>,
      IProductionFormulaRepository
{
    private readonly IRepository<AppProduct, Guid> _productRepository;

    public ProductionFormulaRepository(
        IDbContextProvider<ProductionDbContext> dbContextProvider,
        IRepository<AppProduct, Guid> productRepository)
        : base(dbContextProvider)
    {
        _productRepository = productRepository;
    }

    public async Task<AppProductionFormula> GetWithItemsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await WithDetailsAsync(f => f.Items);
        return await query
            .Where(f => f.Id == id)
            .FirstAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<long> CountFilteredAsync(
        string? filter,
        Guid? finishedProductId,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, finishedProductId, isActive);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<ProductionFormulaListItem>> GetFilteredListAsync(
        string? filter,
        Guid? finishedProductId,
        bool? isActive,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);

        var query = await BuildFilteredQueryAsync(filter, finishedProductId, isActive);

        // Phase 3 status/work-center metadata lives in ABP ExtraProperties so approved
        // revisions remain migration-free. Materialize the aggregate before reading
        // those computed properties; EF cannot translate them into SQL.
        var headers = await query
            .Include(f => f.Items)
            .ToListAsync(ct);

        if (headers.Count == 0)
        {
            return new List<ProductionFormulaListItem>();
        }

        var productIds = headers.Select(h => h.FinishedProductId).Distinct().ToList();
        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id), cancellationToken: ct);
        var productById = products.ToDictionary(p => p.Id);

        var rows = headers.Select(h =>
        {
            productById.TryGetValue(h.FinishedProductId, out var product);
            return new ProductionFormulaListItem
            {
                Id = h.Id,
                FinishedProductId = h.FinishedProductId,
                FinishedProductName = product?.Name ?? h.FinishedProductId.ToString(),
                FinishedProductUnit = product?.Unit ?? string.Empty,
                FormulaName = h.FormulaName,
                Version = h.Version,
                OutputQuantity = h.OutputQuantity,
                ExpectedWastePercent = h.ExpectedWastePercent,
                LaborCostPerBatch = h.LaborCostPerBatch,
                OverheadCostPerBatch = h.OverheadCostPerBatch,
                EstimatedProductionMinutes = h.EstimatedProductionMinutes,
                IsActive = h.IsActive,
                IsDefault = h.IsDefault,
                ApprovalStatus = h.ApprovalStatus,
                WorkCenterCode = h.WorkCenterCode,
                ItemCount = h.Items.Count
            };
        });

        return ApplySorting(rows, sorting)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToList();
    }

    public async Task<AppProductionFormula?> GetActiveDefaultForProductAsync(
        Guid finishedProductId,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query
            .Where(f => f.FinishedProductId == finishedProductId && f.IsActive && f.IsDefault)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<int> GetNextVersionAsync(
        Guid finishedProductId,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        var current = await query
            .Where(f => f.FinishedProductId == finishedProductId)
            .Select(f => (int?)f.Version)
            .MaxAsync(GetCancellationToken(cancellationToken));
        return (current ?? 0) + 1;
    }

    public async Task<bool> VersionExistsAsync(
        Guid finishedProductId,
        int version,
        Guid? excludingFormulaId = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        return await query.AnyAsync(
            f => f.FinishedProductId == finishedProductId
                 && f.Version == version
                 && (!excludingFormulaId.HasValue || f.Id != excludingFormulaId.Value),
            GetCancellationToken(cancellationToken));
    }

    public async Task<Dictionary<Guid, decimal>> GetIngredientCostPricesAsync(
        IReadOnlyCollection<Guid> ingredientProductIds,
        CancellationToken cancellationToken = default)
    {
        if (ingredientProductIds.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var ids = ingredientProductIds.Distinct().ToList();
        var products = await _productRepository.GetListAsync(
            p => ids.Contains(p.Id), cancellationToken: GetCancellationToken(cancellationToken));

        return products.ToDictionary(p => p.Id, p => p.CostPrice);
    }

    public async Task<List<ProductionProductLookup>> GetProducibleFinishedProductsLookupAsync(
        string? filter,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);
        var query = await _productRepository.GetQueryableAsync();

        query = query.Where(p => p.IsActive && p.IsProducible);
        query = ApplyProductFilter(query, filter);

        var products = await query
            .OrderBy(p => p.Name)
            .Take(maxResults)
            .ToListAsync(ct);

        return products.ConvertAll(MapLookup);
    }

    public async Task<List<ProductionProductLookup>> GetIngredientProductsLookupAsync(
        string? filter,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);
        var query = await _productRepository.GetQueryableAsync();

        query = query.Where(p => p.IsActive
            && (p.ProductType == ProductTypes.RawMaterial
                || p.ProductType == ProductTypes.Packaging
                || p.ProductType == ProductTypes.SemiFinished));
        query = ApplyProductFilter(query, filter);

        var products = await query
            .OrderBy(p => p.Name)
            .Take(maxResults)
            .ToListAsync(ct);

        return products.ConvertAll(MapLookup);
    }

    // ── Helpers ──

    private async Task<IQueryable<AppProductionFormula>> BuildFilteredQueryAsync(
        string? filter,
        Guid? finishedProductId,
        bool? isActive)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(x => x.FormulaName.ToLower().Contains(f));
        }

        if (finishedProductId.HasValue)
        {
            query = query.Where(x => x.FinishedProductId == finishedProductId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        return query;
    }

    private static IQueryable<AppProduct> ApplyProductFilter(IQueryable<AppProduct> query, string? filter)
    {
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(f) || p.SKU.ToLower().Contains(f));
        }
        return query;
    }

    private static ProductionProductLookup MapLookup(AppProduct p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        SKU = p.SKU,
        Unit = p.Unit,
        ProductType = p.ProductType,
        CostPrice = p.CostPrice,
        Currency = p.Currency
    };

    private static IEnumerable<ProductionFormulaListItem> ApplySorting(
        IEnumerable<ProductionFormulaListItem> rows,
        string sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
        {
            return rows
                .OrderBy(r => r.FinishedProductName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.FormulaName, StringComparer.OrdinalIgnoreCase);
        }

        var parts = sorting.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var field = parts[0];
        var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        Func<ProductionFormulaListItem, object?> keySelector = field.ToLowerInvariant() switch
        {
            "formulaname" => r => r.FormulaName,
            "finishedproductname" => r => r.FinishedProductName,
            "version" => r => r.Version,
            "outputquantity" => r => r.OutputQuantity,
            "itemcount" => r => r.ItemCount,
            "isactive" => r => r.IsActive,
            "isdefault" => r => r.IsDefault,
            _ => r => r.FormulaName
        };

        return descending
            ? rows.OrderByDescending(keySelector)
            : rows.OrderBy(keySelector);
    }

}
