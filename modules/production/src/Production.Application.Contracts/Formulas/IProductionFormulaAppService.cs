using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Production.Formulas;

public interface IProductionFormulaAppService : IApplicationService
{
    Task<ProductionFormulaDto> GetAsync(Guid id);

    Task<PagedResultDto<ProductionFormulaListItemDto>> GetListAsync(GetProductionFormulasInput input);

    Task<ProductionFormulaDto> CreateAsync(CreateProductionFormulaDto input);

    Task<ProductionFormulaDto> UpdateAsync(Guid id, UpdateProductionFormulaDto input);

    Task DeleteAsync(Guid id);

    /// <summary>Deterministic planned-cost preview for a saved formula at the given target output quantity.</summary>
    Task<PlannedCostDto> CalculatePlannedCostAsync(Guid formulaId, int plannedOutputQuantity);

    /// <summary>
    /// Deterministic planned-cost preview for an unsaved (draft) formula. Lets the editor
    /// show a live cost while the user is still building the formula, without persisting
    /// anything. Same calculator as <see cref="CalculatePlannedCostAsync"/>.
    /// </summary>
    Task<PlannedCostDto> PreviewPlannedCostAsync(CreateProductionFormulaDto input, int plannedOutputQuantity);

    /// <summary>Active producible finished products (for the finished-product dropdown).</summary>
    Task<List<ProductLookupDto>> GetProducibleFinishedProductsLookupAsync(string? filter = null);

    /// <summary>Active ingredient products — RawMaterial / Packaging / SemiFinished (for the items dropdown).</summary>
    Task<List<ProductLookupDto>> GetIngredientProductsLookupAsync(string? filter = null);
}
