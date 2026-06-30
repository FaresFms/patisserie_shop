using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
using Microsoft.AspNetCore.Authorization;
using Production.Entities;
using Production.Formulas;
using Production.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Production.BranchRequests;

[Authorize(ProductionPermissions.BranchRequests.Default)]
public class BranchProductionRequestAppService : ProductionAppService, IBranchProductionRequestAppService
{
    private readonly IBranchProductionRequestRepository _requestRepository;
    private readonly BranchProductionRequestManager _requestManager;
    private readonly IProductionFormulaRepository _formulaRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IAuthorizationService _authorizationService;

    public BranchProductionRequestAppService(
        IBranchProductionRequestRepository requestRepository,
        BranchProductionRequestManager requestManager,
        IProductionFormulaRepository formulaRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        IAuthorizationService authorizationService)
    {
        _requestRepository = requestRepository;
        _requestManager = requestManager;
        _formulaRepository = formulaRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _authorizationService = authorizationService;
    }

    public async Task<BranchProductionRequestDto> GetAsync(Guid id)
    {
        var request = await _requestRepository.GetWithItemsAsync(id);
        await EnsureCanSeeRequestAsync(request);
        return await MapToDtoAsync(request);
    }

    public async Task<PagedResultDto<BranchProductionRequestListItemDto>> GetListAsync(GetBranchProductionRequestsInput input)
    {
        var scopedBranchIds = await GetBranchScopeAsync();

        var totalCount = await _requestRepository.CountFilteredAsync(
            input.Filter, input.Status, input.BranchId, scopedBranchIds);

        var items = await _requestRepository.GetFilteredListAsync(
            input.Filter,
            input.Status,
            input.BranchId,
            scopedBranchIds,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        var dtos = new List<BranchProductionRequestListItemDto>();
        foreach (var item in items)
        {
            dtos.Add(ObjectMapper.Map<BranchProductionRequestListItem, BranchProductionRequestListItemDto>(item));
        }

        return new PagedResultDto<BranchProductionRequestListItemDto>(totalCount, dtos);
    }

    public async Task<BranchProductionRequestDto> CreateAsync(CreateBranchProductionRequestDto input)
    {
        await _requestManager.EnsureBranchAccessAsync(input.BranchId, CurrentUser.Id, await CanReviewAllRequestsAsync());
        await _requestManager.EnsureRequestProductsAreProducibleAsync(GetProductIds(input.Items));

        var request = await _requestManager.CreateAsync(
            input.BranchId,
            input.NeededByDate,
            input.Priority,
            CurrentUser.Id,
            input.Notes);

        ApplyItems(request, input.Items);

        await _requestRepository.InsertAsync(request, autoSave: true);
        return await MapToDtoAsync(request);
    }

    public async Task<BranchProductionRequestDto> UpdateAsync(Guid id, UpdateBranchProductionRequestDto input)
    {
        var request = await _requestRepository.GetWithItemsAsync(id);
        await _requestManager.EnsureBranchAccessAsync(request.BranchId, CurrentUser.Id, await CanReviewAllRequestsAsync());
        await _requestManager.EnsureRequestProductsAreProducibleAsync(GetProductIds(input.Items));

        request.UpdateHeader(input.NeededByDate, input.Priority, input.Notes);
        request.ClearItems();
        ApplyItems(request, input.Items);

        await _requestRepository.UpdateAsync(request, autoSave: true);
        return await MapToDtoAsync(request);
    }

    public async Task<BranchProductionRequestDto> SubmitAsync(Guid id)
    {
        var request = await _requestRepository.GetWithItemsAsync(id);
        await _requestManager.EnsureBranchAccessAsync(request.BranchId, CurrentUser.Id, await CanReviewAllRequestsAsync());

        request.Submit();
        await _requestRepository.UpdateAsync(request, autoSave: true);
        return await MapToDtoAsync(request);
    }

    public async Task<BranchProductionRequestDto> CancelAsync(Guid id)
    {
        var request = await _requestRepository.GetWithItemsAsync(id);
        await _requestManager.EnsureBranchAccessAsync(request.BranchId, CurrentUser.Id, await CanReviewAllRequestsAsync());

        request.Cancel();
        await _requestRepository.UpdateAsync(request, autoSave: true);
        return await MapToDtoAsync(request);
    }

    [Authorize(ProductionPermissions.BranchRequests.Approve)]
    public async Task<BranchProductionRequestDto> ApproveAsync(Guid id, ApproveBranchProductionRequestDto input)
    {
        var request = await _requestRepository.GetWithItemsAsync(id);
        var approved = new Dictionary<Guid, int>();
        foreach (var item in input.Items)
        {
            approved[item.ItemId] = item.ApprovedQuantity;
        }

        request.Approve(CurrentUser.Id, approved, input.Reason);
        await _requestRepository.UpdateAsync(request, autoSave: true);
        return await MapToDtoAsync(request);
    }

    [Authorize(ProductionPermissions.BranchRequests.Reject)]
    public async Task<BranchProductionRequestDto> RejectAsync(Guid id, RejectBranchProductionRequestDto input)
    {
        var request = await _requestRepository.GetWithItemsAsync(id);
        request.Reject(CurrentUser.Id, input.Reason);
        await _requestRepository.UpdateAsync(request, autoSave: true);
        return await MapToDtoAsync(request);
    }

    public async Task<List<ProductLookupDto>> GetRequestableProductsLookupAsync(string? filter = null)
    {
        var products = await _formulaRepository.GetProducibleFinishedProductsLookupAsync(filter, maxResults: 200);
        var dtos = new List<ProductLookupDto>();
        foreach (var product in products)
        {
            dtos.Add(ObjectMapper.Map<ProductionProductLookup, ProductLookupDto>(product));
        }
        return dtos;
    }

    private async Task<IReadOnlyCollection<Guid>?> GetBranchScopeAsync()
    {
        if (await CanReviewAllRequestsAsync())
        {
            return null;
        }

        var branches = await _branchRepository.GetListAsync(b => b.ManagerUserId == CurrentUser.Id);
        var ids = new List<Guid>();
        foreach (var branch in branches)
        {
            ids.Add(branch.Id);
        }
        return ids;
    }

    private async Task EnsureCanSeeRequestAsync(AppBranchProductionRequest request)
    {
        if (await CanReviewAllRequestsAsync())
        {
            return;
        }

        await _requestManager.EnsureBranchAccessAsync(request.BranchId, CurrentUser.Id, bypass: false);
    }

    private Task<bool> CanReviewAllRequestsAsync() =>
        _authorizationService.IsGrantedAsync(ProductionPermissions.BranchRequests.Approve);

    private void ApplyItems(AppBranchProductionRequest request, List<CreateBranchProductionRequestItemDto> items)
    {
        foreach (var item in items)
        {
            request.AddItem(GuidGenerator.Create(), item.ProductId, item.RequestedQuantity, item.Notes);
        }
    }

    private static List<Guid> GetProductIds(List<CreateBranchProductionRequestItemDto> items)
    {
        var ids = new List<Guid>();
        foreach (var item in items)
        {
            ids.Add(item.ProductId);
        }
        return ids;
    }

    private async Task<BranchProductionRequestDto> MapToDtoAsync(AppBranchProductionRequest request)
    {
        var dto = ObjectMapper.Map<AppBranchProductionRequest, BranchProductionRequestDto>(request);

        var branch = await _branchRepository.FindAsync(request.BranchId);
        dto.BranchName = branch?.Name;

        var productIds = new List<Guid>();
        foreach (var item in request.Items)
        {
            productIds.Add(item.ProductId);
        }

        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id));
        var productById = new Dictionary<Guid, AppProduct>();
        foreach (var product in products)
        {
            productById[product.Id] = product;
        }

        foreach (var item in dto.Items)
        {
            if (productById.TryGetValue(item.ProductId, out var product))
            {
                item.ProductName = product.Name;
                item.ProductSku = product.SKU;
                item.ProductUnit = product.Unit;
            }
        }

        return dto;
    }
}
