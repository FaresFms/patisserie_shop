using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Intelligence.Entities;
using Intelligence.Permissions;
using Inventory.BranchInventory;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Intelligence.Velocity;

[Authorize(IntelligencePermissions.DecisionLogs.Default)]
public class ProductVelocityAppService : IntelligenceAppService, IProductVelocityAppService
{
    private readonly IProductVelocityRepository _velocityRepository;
    private readonly IBranchInventoryAppService _branchInventoryAppService;
    private readonly IAuthorizationService _authorizationService;

    public ProductVelocityAppService(
        IProductVelocityRepository velocityRepository,
        IBranchInventoryAppService branchInventoryAppService,
        IAuthorizationService authorizationService)
    {
        _velocityRepository = velocityRepository;
        _branchInventoryAppService = branchInventoryAppService;
        _authorizationService = authorizationService;
    }

    public async Task<PagedResultDto<ProductVelocityDto>> GetListAsync(GetProductVelocitiesInput input)
    {
        var scope = await GetBranchScopeAsync();

        var totalCount = await _velocityRepository.CountFilteredAsync(
            input.Filter, input.BranchId, scope);

        var items = await _velocityRepository.GetFilteredListAsync(
            input.Filter, input.BranchId, scope,
            input.Sorting ?? string.Empty, input.SkipCount, input.MaxResultCount);

        return new PagedResultDto<ProductVelocityDto>(totalCount, items.ConvertAll(BuildDto));
    }

    private ProductVelocityDto BuildDto(ProductVelocityListRow row)
    {
        var dto = ObjectMapper.Map<AppProductVelocity, ProductVelocityDto>(row.Velocity);
        dto.ProductName = row.ProductName;
        dto.ProductSku = row.ProductSku;
        dto.BranchName = row.BranchName;
        dto.CurrentStock = row.CurrentStock;
        dto.DaysOfCover = row.DaysOfCover;
        return dto;
    }

    /// <summary>
    /// Returns the branch-ID set the current user is allowed to see (mirrors
    /// DecisionLogAppService). Null means "no scope" (admin / ManageAll) and the
    /// repository skips the branch filter entirely.
    /// </summary>
    private async Task<IReadOnlyCollection<Guid>?> GetBranchScopeAsync()
    {
        if (await _authorizationService.IsGrantedAsync(InventoryPermissions.BranchInventory.ManageAll))
        {
            return null;
        }

        var ids = await _branchInventoryAppService.GetAccessibleBranchIdsAsync();
        return ids;
    }
}
