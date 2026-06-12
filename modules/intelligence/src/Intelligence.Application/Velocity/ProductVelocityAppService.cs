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

    /// <summary>
    /// Horizon of the days-of-cover forecast walk on the read side. Wider than the
    /// scanners' 30-day alerting cap so the grid can still show large cover values;
    /// beyond it the flat-division figure is shown instead.
    /// </summary>
    private const int DisplayForecastHorizonDays = 365;

    private ProductVelocityDto BuildDto(ProductVelocityListRow row)
    {
        var dto = ObjectMapper.Map<AppProductVelocity, ProductVelocityDto>(row.Velocity);
        dto.ProductName = row.ProductName;
        dto.ProductSku = row.ProductSku;
        dto.BranchName = row.BranchName;
        dto.CurrentStock = row.CurrentStock;
        dto.DaysOfCover = row.DaysOfCover;
        OverlayForecast(dto, row);
        return dto;
    }

    /// <summary>
    /// Plain arithmetic on the already-loaded row (no queries): sums the next-7-days
    /// weekday-indexed forecast and replaces the flat days-of-cover with the
    /// forecast-walk value when a weekday pattern exists.
    /// </summary>
    private void OverlayForecast(ProductVelocityDto dto, ProductVelocityListRow row)
    {
        var velocity = row.Velocity;
        if (velocity.AvgDailySales30 <= 0)
        {
            return; // no demand signal — Next7DaysForecast and DaysOfCover stay null
        }

        var indices = velocity.GetWeekdayIndices();
        var tomorrowUtc = Clock.Now.ToUniversalTime().Date.AddDays(1);

        var next7 = 0m;
        for (var day = 0; day < 7; day++)
        {
            next7 += ForecastWalker.DailyDemand(
                velocity.AvgDailySales30, indices, tomorrowUtc.AddDays(day).DayOfWeek);
        }
        dto.Next7DaysForecast = Math.Round(next7, 1);

        if (!ForecastWalker.IsFlat(indices))
        {
            var daysUntilDepletion = ForecastWalker.DaysUntilDepletion(
                row.CurrentStock, velocity.AvgDailySales30, indices,
                tomorrowUtc.DayOfWeek, DisplayForecastHorizonDays);

            if (daysUntilDepletion is int days)
            {
                dto.DaysOfCover = days;
            }
            // Survives the whole display horizon → keep the flat-division value
            // from the repository as the (very large) approximation.
        }
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
