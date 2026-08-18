using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Inventory.Entities;
using Microsoft.AspNetCore.Authorization;
using Production.Entities;
using Production.Control;
using Production.Kitchens;
using Production.Formulas;
using Production.Orders;
using Production.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

namespace Production.Plans;

[Authorize(ProductionPermissions.Plans.Default)]
public class ProductionPlanAppService : ProductionAppService, IProductionPlanAppService
{
    private readonly IProductionPlanRepository _planRepository;
    private readonly ProductionPlanManager _planManager;
    private readonly ProductionOrderManager _orderManager;
    private readonly IProductionOrderRepository _orderRepository;
    private readonly IProductionFormulaRepository _formulaRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly KitchenAccessChecker _kitchenAccessChecker;
    private readonly ISettingProvider _settingProvider;

    public ProductionPlanAppService(
        IProductionPlanRepository planRepository,
        ProductionPlanManager planManager,
        ProductionOrderManager orderManager,
        IProductionOrderRepository orderRepository,
        IProductionFormulaRepository formulaRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        KitchenAccessChecker kitchenAccessChecker,
        ISettingProvider settingProvider)
    {
        _planRepository = planRepository;
        _planManager = planManager;
        _orderManager = orderManager;
        _orderRepository = orderRepository;
        _formulaRepository = formulaRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _kitchenAccessChecker = kitchenAccessChecker;
        _settingProvider = settingProvider;
    }

    public async Task<ProductionPlanDto> GetAsync(Guid id)
    {
        var plan = await _planRepository.GetWithLinesAsync(id);
        await _kitchenAccessChecker.EnsureAccessAsync(plan.KitchenBranchId);
        return await MapToDtoAsync(plan);
    }

    public async Task<PagedResultDto<ProductionPlanListItemDto>> GetListAsync(GetProductionPlansInput input)
    {
        var kitchenIds = await GetKitchenScopeAsync(input.KitchenBranchId);
        var totalCount = await _planRepository.CountFilteredAsync(
            input.Filter, input.Status, input.KitchenBranchId, kitchenIds);

        var items = await _planRepository.GetFilteredListAsync(
            input.Filter,
            input.Status,
            input.KitchenBranchId,
            kitchenIds,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        var dtos = new List<ProductionPlanListItemDto>();
        foreach (var item in items)
        {
            dtos.Add(ObjectMapper.Map<ProductionPlanListItem, ProductionPlanListItemDto>(item));
        }

        return new PagedResultDto<ProductionPlanListItemDto>(totalCount, dtos);
    }

    [Authorize(ProductionPermissions.Plans.Manage)]
    public async Task<ProductionPlanDto> CreateDraftAsync(CreateProductionPlanDto input)
    {
        await _kitchenAccessChecker.EnsureAccessAsync(input.KitchenBranchId);
        var controlProfile = await GetControlProfileAsync();
        var plan = await _planManager.CreateDraftAsync(
            input.KitchenBranchId,
            input.ProductionDate,
            CurrentUser.Id,
            input.Notes,
            controlProfile.ForecastSafetyPercent);

        await _planRepository.InsertAsync(plan, autoSave: true);
        return await MapToDtoAsync(plan);
    }

    private async Task<ProductionControlProfileDto> GetControlProfileAsync()
    {
        var json = await _settingProvider.GetOrNullAsync(ProductionControlSettings.Profile);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ProductionControlProfileDto();
        }
        try
        {
            return JsonSerializer.Deserialize<ProductionControlProfileDto>(
                       json,
                       new JsonSerializerOptions(JsonSerializerDefaults.Web))
                   ?? new ProductionControlProfileDto();
        }
        catch (JsonException)
        {
            return new ProductionControlProfileDto();
        }
    }

    [Authorize(ProductionPermissions.Plans.Manage)]
    public async Task<ProductionPlanDto> UpdateLineAsync(Guid id, Guid lineId, UpdateProductionPlanLineDto input)
    {
        var plan = await _planRepository.GetWithLinesAsync(id);
        await _kitchenAccessChecker.EnsureAccessAsync(plan.KitchenBranchId);
        plan.UpdateLinePlannedQuantity(lineId, input.PlannedQuantity, input.OverrideReason);
        await _planRepository.UpdateAsync(plan, autoSave: true);
        return await MapToDtoAsync(plan);
    }

    [Authorize(ProductionPermissions.Plans.Manage)]
    public async Task<ProductionPlanDto> ConfirmAsync(Guid id)
    {
        var plan = await _planRepository.GetWithLinesAsync(id);
        await _kitchenAccessChecker.EnsureAccessAsync(plan.KitchenBranchId);
        await _planManager.ConfirmAsync(plan, CurrentUser.Id);

        var orders = await _orderManager.CreateFromPlanAsync(plan, CurrentUser.Id);
        foreach (var order in orders)
        {
            await _orderRepository.InsertAsync(order);
        }

        await _planRepository.UpdateAsync(plan, autoSave: true);
        return await MapToDtoAsync(plan);
    }

    [Authorize(ProductionPermissions.Plans.Manage)]
    public async Task<ProductionPlanDto> CancelAsync(Guid id)
    {
        var plan = await _planRepository.GetWithLinesAsync(id);
        await _kitchenAccessChecker.EnsureAccessAsync(plan.KitchenBranchId);
        await _planManager.CancelAsync(plan);
        await _planRepository.UpdateAsync(plan, autoSave: true);
        return await MapToDtoAsync(plan);
    }

    private async Task<ProductionPlanDto> MapToDtoAsync(AppProductionPlan plan)
    {
        var dto = ObjectMapper.Map<AppProductionPlan, ProductionPlanDto>(plan);

        var branch = await _branchRepository.FindAsync(plan.KitchenBranchId);
        dto.KitchenBranchName = branch?.DisplayName;

        var productIds = new List<Guid>();
        foreach (var line in plan.Lines)
        {
            productIds.Add(line.ProductId);
        }

        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id));
        var productById = new Dictionary<Guid, AppProduct>();
        foreach (var product in products)
        {
            productById[product.Id] = product;
        }

        foreach (var line in dto.Lines)
        {
            line.HasActiveDefaultFormula =
                await _formulaRepository.GetActiveDefaultForProductAsync(line.ProductId) != null;

            if (productById.TryGetValue(line.ProductId, out var product))
            {
                line.ProductName = product.DisplayName;
                line.ProductSku = product.SKU;
                line.ProductUnit = product.DisplayUnit;
            }
        }

        return dto;
    }

    private async Task<List<Guid>> GetKitchenScopeAsync(Guid? kitchenBranchId)
    {
        if (kitchenBranchId.HasValue)
        {
            await _kitchenAccessChecker.EnsureAccessAsync(kitchenBranchId.Value);
        }

        return await _kitchenAccessChecker.GetAccessibleKitchenIdsAsync();
    }
}
