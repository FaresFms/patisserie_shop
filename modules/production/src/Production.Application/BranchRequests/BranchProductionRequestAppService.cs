using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Settings;
using Microsoft.AspNetCore.Authorization;
using Production.Entities;
using Production.Formulas;
using Production.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

namespace Production.BranchRequests;

[Authorize]
public class BranchProductionRequestAppService : ProductionAppService, IBranchProductionRequestAppService
{
    private readonly IBranchProductionRequestRepository _requestRepository;
    private readonly BranchProductionRequestManager _requestManager;
    private readonly IProductionFormulaRepository _formulaRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly ISettingProvider _settingProvider;

    public BranchProductionRequestAppService(
        IBranchProductionRequestRepository requestRepository,
        BranchProductionRequestManager requestManager,
        IProductionFormulaRepository formulaRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        ISettingProvider settingProvider)
    {
        _requestRepository = requestRepository;
        _requestManager = requestManager;
        _formulaRepository = formulaRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _settingProvider = settingProvider;
    }

    [Authorize(ProductionPermissions.BranchRequests.ViewAll)]
    public Task<BranchProductionRequestDto> GetAsync(Guid id)
        => GetForReviewAsync(id);

    [Authorize(ProductionPermissions.BranchRequests.ViewAll)]
    public Task<PagedResultDto<BranchProductionRequestListItemDto>> GetListAsync(GetBranchProductionRequestsInput input)
        => GetReviewListAsync(input);

    [Authorize(ProductionPermissions.MyRequests.Default)]
    public async Task<BranchProductionRequestDto> GetMyAsync(Guid id)
    {
        var request = await _requestRepository.GetWithItemsAsync(id);
        await _requestManager.EnsureBranchAccessAsync(request.BranchId, CurrentUser.Id, bypass: false);
        return await MapToDtoAsync(request);
    }

    [Authorize(ProductionPermissions.MyRequests.Default)]
    public async Task<PagedResultDto<BranchProductionRequestListItemDto>> GetMyListAsync(GetBranchProductionRequestsInput input)
    {
        var scopedBranchIds = await GetManagedBranchIdsAsync();

        return await GetListInternalAsync(input, scopedBranchIds);
    }

    [Authorize(ProductionPermissions.BranchRequests.ViewAll)]
    public async Task<BranchProductionRequestDto> GetForReviewAsync(Guid id)
    {
        var request = await _requestRepository.GetWithItemsAsync(id);
        return await MapToDtoAsync(request);
    }

    [Authorize(ProductionPermissions.BranchRequests.ViewAll)]
    public Task<PagedResultDto<BranchProductionRequestListItemDto>> GetReviewListAsync(
        GetBranchProductionRequestsInput input)
        => GetListInternalAsync(input, scopedBranchIds: null);

    private async Task<PagedResultDto<BranchProductionRequestListItemDto>> GetListInternalAsync(
        GetBranchProductionRequestsInput input,
        IReadOnlyCollection<Guid>? scopedBranchIds)
    {

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

    [Authorize(ProductionPermissions.MyRequests.Default)]
    public async Task<BranchProductionRequestDto> CreateAsync(CreateBranchProductionRequestDto input)
    {
        await _requestManager.EnsureBranchAccessAsync(input.BranchId, CurrentUser.Id, bypass: false);
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

    [Authorize(ProductionPermissions.MyRequests.Default)]
    public async Task<BranchProductionRequestDto> UpdateAsync(Guid id, UpdateBranchProductionRequestDto input)
    {
        var request = await _requestRepository.GetWithItemsAsync(id);
        await _requestManager.EnsureBranchAccessAsync(request.BranchId, CurrentUser.Id, bypass: false);
        await _requestManager.EnsureRequestProductsAreProducibleAsync(GetProductIds(input.Items));

        request.UpdateHeader(input.NeededByDate, input.Priority, input.Notes);
        request.ClearItems();
        ApplyItems(request, input.Items);

        await _requestRepository.UpdateAsync(request, autoSave: true);
        return await MapToDtoAsync(request);
    }

    [Authorize(ProductionPermissions.MyRequests.Default)]
    public async Task<BranchProductionRequestDto> SubmitAsync(Guid id)
    {
        var request = await _requestRepository.GetWithItemsAsync(id);
        await _requestManager.EnsureBranchAccessAsync(request.BranchId, CurrentUser.Id, bypass: false);

        request.Submit();
        await _requestRepository.UpdateAsync(request, autoSave: true);
        return await MapToDtoAsync(request);
    }

    [Authorize(ProductionPermissions.MyRequests.Default)]
    public async Task<BranchProductionRequestDto> CancelAsync(Guid id)
    {
        var request = await _requestRepository.GetWithItemsAsync(id);
        await _requestManager.EnsureBranchAccessAsync(request.BranchId, CurrentUser.Id, bypass: false);

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

    [Authorize(ProductionPermissions.MyRequests.Default)]
    public async Task<List<RequestableBranchLookupDto>> GetRequestableBranchesLookupAsync()
    {
        var branches = await _requestManager.GetRequestableBranchesAsync(
            CurrentUser.Id,
            bypass: false);

        var dtos = new List<RequestableBranchLookupDto>(branches.Count);
        foreach (var branch in branches)
        {
            dtos.Add(new RequestableBranchLookupDto
            {
                Id = branch.Id,
                Name = branch.Name
            });
        }

        return dtos;
    }

    [Authorize(ProductionPermissions.MyRequests.Default)]
    public async Task<List<ProductLookupDto>> GetRequestableProductsLookupAsync(string? filter = null)
    {
        var products = await _formulaRepository.GetProducibleFinishedProductsLookupAsync(filter, maxResults: 200);
        var currency = ShopCurrencySettings.Normalize(
            await _settingProvider.GetOrNullAsync(ShopCurrencySettings.Name));
        var dtos = new List<ProductLookupDto>();
        foreach (var product in products)
        {
            var dto = ObjectMapper.Map<ProductionProductLookup, ProductLookupDto>(product);
            dto.Currency = currency;
            dtos.Add(dto);
        }
        return dtos;
    }

    private async Task<IReadOnlyCollection<Guid>> GetManagedBranchIdsAsync()
    {
        var branches = await _branchRepository.GetListAsync(b => b.ManagerUserId == CurrentUser.Id);
        var ids = new List<Guid>();
        foreach (var branch in branches)
        {
            ids.Add(branch.Id);
        }
        return ids;
    }

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
