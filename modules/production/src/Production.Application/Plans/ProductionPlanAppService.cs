using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
using Microsoft.AspNetCore.Authorization;
using Production.Entities;
using Production.Orders;
using Production.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Production.Plans;

[Authorize(ProductionPermissions.Plans.Default)]
public class ProductionPlanAppService : ProductionAppService, IProductionPlanAppService
{
    private readonly IProductionPlanRepository _planRepository;
    private readonly ProductionPlanManager _planManager;
    private readonly ProductionOrderManager _orderManager;
    private readonly IProductionOrderRepository _orderRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;

    public ProductionPlanAppService(
        IProductionPlanRepository planRepository,
        ProductionPlanManager planManager,
        ProductionOrderManager orderManager,
        IProductionOrderRepository orderRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository)
    {
        _planRepository = planRepository;
        _planManager = planManager;
        _orderManager = orderManager;
        _orderRepository = orderRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
    }

    public async Task<ProductionPlanDto> GetAsync(Guid id)
    {
        var plan = await _planRepository.GetWithLinesAsync(id);
        return await MapToDtoAsync(plan);
    }

    public async Task<PagedResultDto<ProductionPlanListItemDto>> GetListAsync(GetProductionPlansInput input)
    {
        var totalCount = await _planRepository.CountFilteredAsync(
            input.Filter, input.Status, input.KitchenBranchId);

        var items = await _planRepository.GetFilteredListAsync(
            input.Filter,
            input.Status,
            input.KitchenBranchId,
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
        var plan = await _planManager.CreateDraftAsync(
            input.KitchenBranchId,
            input.ProductionDate,
            CurrentUser.Id,
            input.Notes);

        await _planRepository.InsertAsync(plan, autoSave: true);
        return await MapToDtoAsync(plan);
    }

    [Authorize(ProductionPermissions.Plans.Manage)]
    public async Task<ProductionPlanDto> UpdateLineAsync(Guid id, Guid lineId, UpdateProductionPlanLineDto input)
    {
        var plan = await _planRepository.GetWithLinesAsync(id);
        plan.UpdateLinePlannedQuantity(lineId, input.PlannedQuantity, input.OverrideReason);
        await _planRepository.UpdateAsync(plan, autoSave: true);
        return await MapToDtoAsync(plan);
    }

    [Authorize(ProductionPermissions.Plans.Manage)]
    public async Task<ProductionPlanDto> ConfirmAsync(Guid id)
    {
        var plan = await _planRepository.GetWithLinesAsync(id);
        plan.Confirm(CurrentUser.Id);

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
        await _planManager.CancelAsync(plan);
        await _planRepository.UpdateAsync(plan, autoSave: true);
        return await MapToDtoAsync(plan);
    }

    private async Task<ProductionPlanDto> MapToDtoAsync(AppProductionPlan plan)
    {
        var dto = ObjectMapper.Map<AppProductionPlan, ProductionPlanDto>(plan);

        var branch = await _branchRepository.FindAsync(plan.KitchenBranchId);
        dto.KitchenBranchName = branch?.Name;

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
            if (productById.TryGetValue(line.ProductId, out var product))
            {
                line.ProductName = product.Name;
                line.ProductSku = product.SKU;
                line.ProductUnit = product.Unit;
            }
        }

        return dto;
    }
}
