using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Production.Costing;
using Production.Entities;
using Production.Permissions;
using Inventory.Settings;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Settings;

namespace Production.Formulas;

[Authorize(ProductionPermissions.Formulas.Default)]
public class ProductionFormulaAppService : ProductionAppService, IProductionFormulaAppService
{
    private readonly IProductionFormulaRepository _formulaRepository;
    private readonly ProductionFormulaManager _formulaManager;
    private readonly ISettingProvider _settingProvider;

    public ProductionFormulaAppService(
        IProductionFormulaRepository formulaRepository,
        ProductionFormulaManager formulaManager,
        ISettingProvider settingProvider)
    {
        _formulaRepository = formulaRepository;
        _formulaManager = formulaManager;
        _settingProvider = settingProvider;
    }

    public async Task<ProductionFormulaDto> GetAsync(Guid id)
    {
        var formula = await _formulaRepository.GetWithItemsAsync(id);
        return await MapToDtoAsync(formula);
    }

    public async Task<PagedResultDto<ProductionFormulaListItemDto>> GetListAsync(GetProductionFormulasInput input)
    {
        var totalCount = await _formulaRepository.CountFilteredAsync(
            input.Filter, input.FinishedProductId, input.IsActive);

        var items = await _formulaRepository.GetFilteredListAsync(
            input.Filter,
            input.FinishedProductId,
            input.IsActive,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        var dtos = items.ConvertAll(i => ObjectMapper.Map<ProductionFormulaListItem, ProductionFormulaListItemDto>(i));
        return new PagedResultDto<ProductionFormulaListItemDto>(totalCount, dtos);
    }

    [Authorize(ProductionPermissions.Formulas.Manage)]
    public async Task<ProductionFormulaDto> CreateAsync(CreateProductionFormulaDto input)
    {
        await _formulaManager.EnsureIngredientsAreValidAsync(input.Items.Select(i => i.IngredientProductId));

        var formula = await _formulaManager.CreateAsync(
            input.FinishedProductId,
            input.FormulaName,
            input.OutputQuantity,
            input.Version,
            input.ExpectedWastePercent,
            input.LaborCostPerBatch,
            input.OverheadCostPerBatch,
            input.EstimatedProductionMinutes,
            input.IsActive,
            input.IsDefault,
            input.Notes);

        ApplyItems(formula, input.Items);

        await _formulaRepository.InsertAsync(formula, autoSave: true);
        return await MapToDtoAsync(formula);
    }

    [Authorize(ProductionPermissions.Formulas.Manage)]
    public async Task<ProductionFormulaDto> UpdateAsync(Guid id, UpdateProductionFormulaDto input)
    {
        var formula = await _formulaRepository.GetWithItemsAsync(id);

        await _formulaManager.EnsureFinishedProductIsProducibleAsync(input.FinishedProductId);
        await _formulaManager.EnsureIngredientsAreValidAsync(input.Items.Select(i => i.IngredientProductId));

        formula.UpdateInfo(
            input.FormulaName,
            input.OutputQuantity,
            input.ExpectedWastePercent,
            input.LaborCostPerBatch,
            input.OverheadCostPerBatch,
            input.EstimatedProductionMinutes,
            input.Notes);
        formula.SetVersion(input.Version);

        if (input.IsActive)
        {
            formula.Activate();
        }
        else
        {
            formula.Deactivate();
        }

        formula.ClearItems();
        ApplyItems(formula, input.Items);

        if (input.IsDefault)
        {
            await _formulaManager.MarkDefaultAsync(formula);
        }
        else
        {
            formula.UnmarkDefault();
        }

        await _formulaRepository.UpdateAsync(formula, autoSave: true);
        return await MapToDtoAsync(formula);
    }

    [Authorize(ProductionPermissions.Formulas.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        await _formulaRepository.DeleteAsync(id);
    }

    public async Task<PlannedCostDto> CalculatePlannedCostAsync(Guid formulaId, int plannedOutputQuantity)
    {
        var formula = await _formulaRepository.GetWithItemsAsync(formulaId);
        return await BuildPlannedCostAsync(formula, plannedOutputQuantity);
    }

    public async Task<PlannedCostDto> PreviewPlannedCostAsync(CreateProductionFormulaDto input, int plannedOutputQuantity)
    {
        var formula = _formulaManager.BuildTransient(
            input.FinishedProductId,
            input.FormulaName,
            input.OutputQuantity,
            input.ExpectedWastePercent,
            input.LaborCostPerBatch,
            input.OverheadCostPerBatch,
            input.EstimatedProductionMinutes,
            input.Items.Select(i => (i.IngredientProductId, i.Quantity, i.LossPercent, i.SortOrder)));

        return await BuildPlannedCostAsync(formula, plannedOutputQuantity);
    }

    private async Task<PlannedCostDto> BuildPlannedCostAsync(AppProductionFormula formula, int plannedOutputQuantity)
    {
        var ingredientIds = formula.Items.Select(i => i.IngredientProductId).Distinct().ToList();
        var unitCosts = await _formulaRepository.GetIngredientCostPricesAsync(ingredientIds);

        var result = ProductionCostCalculator.Calculate(formula, plannedOutputQuantity, unitCosts);

        // Overlay ingredient names/units from the same lookup the editor uses.
        var ingredientLookup = await _formulaRepository.GetIngredientProductsLookupAsync(filter: null, maxResults: 1000);
        var lookupById = ingredientLookup.ToDictionary(p => p.Id);

        return new PlannedCostDto
        {
            FormulaId = formula.Id,
            PlannedOutputQuantity = result.PlannedOutputQuantity,
            Batches = result.Batches,
            PlannedIngredientCost = result.PlannedIngredientCost,
            LaborCost = result.LaborCost,
            OverheadCost = result.OverheadCost,
            PlannedTotalCost = result.PlannedTotalCost,
            PlannedUnitCost = result.PlannedUnitCost,
            Currency = await GetShopCurrencyAsync(),
            HasZeroCostIngredient = result.HasZeroCostIngredient,
            Lines = result.Lines.Select(line =>
            {
                lookupById.TryGetValue(line.IngredientProductId, out var product);
                return new PlannedCostLineDto
                {
                    IngredientProductId = line.IngredientProductId,
                    IngredientProductName = product?.Name,
                    IngredientUnit = product?.Unit,
                    RequiredQuantity = line.RequiredQuantity,
                    UnitCost = line.UnitCost,
                    LineCost = line.LineCost,
                    HasZeroCost = line.HasZeroCost
                };
            }).ToList()
        };
    }

    public async Task<List<ProductLookupDto>> GetProducibleFinishedProductsLookupAsync(string? filter = null)
    {
        var products = await _formulaRepository.GetProducibleFinishedProductsLookupAsync(filter, maxResults: 200);
        return await MapProductLookupsAsync(products);
    }

    public async Task<List<ProductLookupDto>> GetIngredientProductsLookupAsync(string? filter = null)
    {
        var products = await _formulaRepository.GetIngredientProductsLookupAsync(filter, maxResults: 200);
        return await MapProductLookupsAsync(products);
    }

    private async Task<List<ProductLookupDto>> MapProductLookupsAsync(List<ProductionProductLookup> products)
    {
        var currency = await GetShopCurrencyAsync();
        return products.ConvertAll(product =>
        {
            var dto = ObjectMapper.Map<ProductionProductLookup, ProductLookupDto>(product);
            dto.Currency = currency;
            return dto;
        });
    }

    private async Task<string> GetShopCurrencyAsync()
        => ShopCurrencySettings.Normalize(
            await _settingProvider.GetOrNullAsync(ShopCurrencySettings.Name));

    // ── Helpers ──

    private void ApplyItems(AppProductionFormula formula, List<CreateProductionFormulaItemDto> items)
    {
        var sortOrder = 0;
        foreach (var item in items.OrderBy(i => i.SortOrder))
        {
            formula.AddItem(
                GuidGenerator.Create(),
                item.IngredientProductId,
                item.Quantity,
                item.LossPercent,
                item.SortOrder == 0 ? sortOrder : item.SortOrder);
            sortOrder++;
        }
    }

    /// <summary>Maps the aggregate to its DTO and overlays the joined finished-product name/unit.</summary>
    private async Task<ProductionFormulaDto> MapToDtoAsync(AppProductionFormula formula)
    {
        var dto = ObjectMapper.Map<AppProductionFormula, ProductionFormulaDto>(formula);
        dto.Items = dto.Items.OrderBy(i => i.SortOrder).ToList();

        var lookup = await _formulaRepository.GetProducibleFinishedProductsLookupAsync(filter: null, maxResults: 1000);
        var product = lookup.FirstOrDefault(p => p.Id == formula.FinishedProductId);
        if (product != null)
        {
            dto.FinishedProductName = product.Name;
            dto.FinishedProductUnit = product.Unit;
        }

        return dto;
    }
}
