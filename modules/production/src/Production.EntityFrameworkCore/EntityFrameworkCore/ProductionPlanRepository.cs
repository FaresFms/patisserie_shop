using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Intelligence.Entities;
using Intelligence.Velocity;
using Inventory;
using Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Production.Costing;
using Production.Entities;
using Production.Formulas;
using Production.Plans;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Production.EntityFrameworkCore;

public class ProductionPlanRepository
    : EfCoreRepository<ProductionDbContext, AppProductionPlan, Guid>,
      IProductionPlanRepository
{
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppBranchInventory, Guid> _inventoryRepository;
    private readonly IRepository<AppProductVelocity, Guid> _velocityRepository;
    private readonly IProductionFormulaRepository _formulaRepository;

    public ProductionPlanRepository(
        IDbContextProvider<ProductionDbContext> dbContextProvider,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppBranchInventory, Guid> inventoryRepository,
        IRepository<AppProductVelocity, Guid> velocityRepository,
        IProductionFormulaRepository formulaRepository)
        : base(dbContextProvider)
    {
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _inventoryRepository = inventoryRepository;
        _velocityRepository = velocityRepository;
        _formulaRepository = formulaRepository;
    }

    public async Task<AppProductionPlan> GetWithLinesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await WithDetailsAsync(p => p.Lines);
        return await query.FirstAsync(p => p.Id == id, GetCancellationToken(cancellationToken));
    }

    public async Task<long> CountFilteredAsync(
        string? filter,
        string? status,
        Guid? kitchenBranchId,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildFilteredQueryAsync(filter, status, kitchenBranchId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<ProductionPlanListItem>> GetFilteredListAsync(
        string? filter,
        string? status,
        Guid? kitchenBranchId,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);
        var query = await BuildFilteredQueryAsync(filter, status, kitchenBranchId);

        var headers = await query
            .Select(p => new HeaderProjection
            {
                Id = p.Id,
                PlanNumber = p.PlanNumber,
                KitchenBranchId = p.KitchenBranchId,
                ProductionDate = p.ProductionDate,
                Status = p.Status,
                LineCount = p.Lines.Count,
                SuggestedTotalQuantity = p.Lines.Sum(l => l.SuggestedQuantity),
                PlannedTotalQuantity = p.Lines.Sum(l => l.PlannedQuantity),
                EstimatedTotalCost = p.Lines.Sum(l => l.EstimatedTotalCost)
            })
            .ToListAsync(ct);

        if (headers.Count == 0)
        {
            return new List<ProductionPlanListItem>();
        }

        var branchIds = headers.Select(h => h.KitchenBranchId).Distinct().ToList();
        var branches = await _branchRepository.GetListAsync(b => branchIds.Contains(b.Id), cancellationToken: ct);
        var branchById = branches.ToDictionary(b => b.Id);

        var rows = headers.Select(h =>
        {
            branchById.TryGetValue(h.KitchenBranchId, out var branch);
            return new ProductionPlanListItem
            {
                Id = h.Id,
                PlanNumber = h.PlanNumber,
                KitchenBranchId = h.KitchenBranchId,
                KitchenBranchName = branch?.Name ?? h.KitchenBranchId.ToString(),
                ProductionDate = h.ProductionDate,
                Status = h.Status,
                LineCount = h.LineCount,
                SuggestedTotalQuantity = h.SuggestedTotalQuantity,
                PlannedTotalQuantity = h.PlannedTotalQuantity,
                EstimatedTotalCost = h.EstimatedTotalCost
            };
        });

        return ApplySorting(rows, sorting)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToList();
    }

    public async Task<List<ProductionPlanSuggestion>> BuildSuggestionsAsync(
        Guid kitchenBranchId,
        DateTime productionDate,
        CancellationToken cancellationToken = default)
    {
        var ct = GetCancellationToken(cancellationToken);
        var dbContext = await GetDbContextAsync();
        var dateEnd = productionDate.Date.AddDays(1);

        var demandStatuses = new[]
        {
            BranchProductionRequestStatuses.Approved,
            BranchProductionRequestStatuses.PartiallyPlanned,
            BranchProductionRequestStatuses.Planned,
            BranchProductionRequestStatuses.PartiallyFulfilled
        };

        var requests = await dbContext.BranchProductionRequests
            .Include(r => r.Items)
            .Where(r => demandStatuses.Contains(r.Status) && r.NeededByDate < dateEnd)
            .ToListAsync(ct);

        var requestedByProduct = requests
            .SelectMany(r => r.Items)
            .GroupBy(i => i.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(i => Math.Max(0, i.ApprovedQuantity - i.PlannedQuantity)));

        var velocityQuery = await _velocityRepository.GetQueryableAsync();
        var velocities = await velocityQuery.ToListAsync(ct);
        var forecastByProduct = velocities
            .GroupBy(v => v.ProductId)
            .ToDictionary(
                g => g.Key,
                g => (int)Math.Ceiling(g.Sum(v => ForecastWalker.DailyDemand(
                    v.AvgDailySales30,
                    v.GetWeekdayIndices(),
                    productionDate.DayOfWeek))));

        var productIds = requestedByProduct.Keys.Union(forecastByProduct.Keys).Distinct().ToList();
        if (productIds.Count == 0)
        {
            return new List<ProductionPlanSuggestion>();
        }

        var products = await _productRepository.GetListAsync(
            p => productIds.Contains(p.Id)
                && p.IsActive
                && p.ProductType == ProductTypes.FinishedGood
                && p.IsProducible,
            cancellationToken: ct);
        var productById = products.ToDictionary(p => p.Id);
        productIds = productById.Keys.ToList();

        var inventoryQuery = await _inventoryRepository.GetQueryableAsync();
        var stockRows = await inventoryQuery
            .Where(i => i.BranchId == kitchenBranchId && productIds.Contains(i.ProductId))
            .ToListAsync(ct);
        var stockByProduct = stockRows.ToDictionary(i => i.ProductId, i => i.QuantityOnHand);

        var suggestions = new List<ProductionPlanSuggestion>();
        foreach (var productId in productIds)
        {
            var product = productById[productId];
            requestedByProduct.TryGetValue(productId, out var requested);
            forecastByProduct.TryGetValue(productId, out var forecast);
            stockByProduct.TryGetValue(productId, out var stock);

            var suggested = Math.Max(0, requested + forecast - stock);
            var suggestion = new ProductionPlanSuggestion
            {
                ProductId = productId,
                ProductName = product.Name,
                ProductSku = product.SKU,
                Unit = product.Unit,
                RequestedQuantity = requested,
                ForecastQuantity = forecast,
                CurrentKitchenStock = stock,
                SuggestedQuantity = suggested
            };

            if (suggested > 0)
            {
                await ApplyCostEstimateAsync(suggestion, suggested);
            }

            suggestions.Add(suggestion);
        }

        return suggestions
            .Where(s => s.SuggestedQuantity > 0 || s.RequestedQuantity > 0)
            .OrderByDescending(s => s.RequestedQuantity)
            .ThenByDescending(s => s.ForecastQuantity)
            .ThenBy(s => s.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task ApplyCostEstimateAsync(ProductionPlanSuggestion suggestion, int plannedQuantity)
    {
        var formula = await _formulaRepository.GetActiveDefaultForProductAsync(suggestion.ProductId);
        if (formula == null)
        {
            return;
        }

        formula = await _formulaRepository.GetWithItemsAsync(formula.Id);
        var ingredientIds = formula.Items.Select(i => i.IngredientProductId).Distinct().ToList();
        var unitCosts = await _formulaRepository.GetIngredientCostPricesAsync(ingredientIds);
        var cost = ProductionCostCalculator.Calculate(formula, plannedQuantity, unitCosts);

        suggestion.HasDefaultFormula = true;
        suggestion.HasZeroCostIngredient = cost.HasZeroCostIngredient;
        suggestion.EstimatedIngredientCost = cost.PlannedIngredientCost;
        suggestion.EstimatedLaborCost = cost.LaborCost;
        suggestion.EstimatedOverheadCost = cost.OverheadCost;
        suggestion.EstimatedTotalCost = cost.PlannedTotalCost;
    }

    private async Task<IQueryable<AppProductionPlan>> BuildFilteredQueryAsync(
        string? filter,
        string? status,
        Guid? kitchenBranchId)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLower();
            query = query.Where(p => p.PlanNumber.ToLower().Contains(f));
        }

        if (!string.IsNullOrWhiteSpace(status) && ProductionPlanStatuses.All.Contains(status))
        {
            query = query.Where(p => p.Status == status);
        }

        if (kitchenBranchId.HasValue)
        {
            query = query.Where(p => p.KitchenBranchId == kitchenBranchId.Value);
        }

        return query;
    }

    private static IEnumerable<ProductionPlanListItem> ApplySorting(IEnumerable<ProductionPlanListItem> rows, string sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
        {
            return rows.OrderByDescending(r => r.ProductionDate).ThenBy(r => r.PlanNumber);
        }

        var parts = sorting.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var field = parts[0];
        var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        Func<ProductionPlanListItem, object?> keySelector = field.ToLowerInvariant() switch
        {
            "plannumber" => r => r.PlanNumber,
            "kitchenbranchname" => r => r.KitchenBranchName,
            "productiondate" => r => r.ProductionDate,
            "status" => r => r.Status,
            "plannedtotalquantity" => r => r.PlannedTotalQuantity,
            "estimatedtotalcost" => r => r.EstimatedTotalCost,
            _ => r => r.ProductionDate
        };

        return descending ? rows.OrderByDescending(keySelector) : rows.OrderBy(keySelector);
    }

    private sealed class HeaderProjection
    {
        public Guid Id { get; set; }
        public string PlanNumber { get; set; } = null!;
        public Guid KitchenBranchId { get; set; }
        public DateTime ProductionDate { get; set; }
        public string Status { get; set; } = null!;
        public int LineCount { get; set; }
        public int SuggestedTotalQuantity { get; set; }
        public int PlannedTotalQuantity { get; set; }
        public decimal EstimatedTotalCost { get; set; }
    }
}
