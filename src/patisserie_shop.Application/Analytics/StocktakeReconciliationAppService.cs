using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Permissions;
using Inventory.Stocktakes;
using Microsoft.AspNetCore.Authorization;
using Operations.Permissions;
using Operations.Sales;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace patisserie_shop.Analytics;

[Authorize(OperationsPermissions.Sales.Default)]
[Authorize(InventoryPermissions.BranchInventory.Adjust)]
public class StocktakeReconciliationAppService
    : patisserie_shopAppService,
      IStocktakeReconciliationAppService
{
    private readonly ISaleRepository _saleRepository;
    private readonly IStocktakeSessionRepository _stocktakeRepository;
    private readonly IBranchInventoryRepository _inventoryRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly BranchAccessChecker _branchAccess;
    private readonly IClock _clock;

    public StocktakeReconciliationAppService(
        ISaleRepository saleRepository,
        IStocktakeSessionRepository stocktakeRepository,
        IBranchInventoryRepository inventoryRepository,
        IRepository<AppProduct, Guid> productRepository,
        BranchAccessChecker branchAccess,
        IClock clock)
    {
        _saleRepository = saleRepository;
        _stocktakeRepository = stocktakeRepository;
        _inventoryRepository = inventoryRepository;
        _productRepository = productRepository;
        _branchAccess = branchAccess;
        _clock = clock;
    }

    public async Task<StocktakeReconciliationDto> GetAsync(GetStocktakeReconciliationInput input)
    {
        var days = NormalizeDays(input.Days);
        var today = _clock.Now.ToUniversalTime().Date;
        var fromInclusive = today.AddDays(-(days - 1));
        var toExclusive = today.AddDays(1);
        var branchScope = await ResolveBranchScopeAsync(input.BranchId);

        var dailySales = await _saleRepository.GetDailySalesAsync(fromInclusive, toExclusive, branchScope);
        var productSales = await _saleRepository.GetProductSalesTotalsAsync(fromInclusive, toExclusive, branchScope);
        var stocktakeSummary = await _stocktakeRepository.GetReconciliationSummaryAsync(
            fromInclusive, toExclusive, branchScope);
        var productVariances = await _stocktakeRepository.GetProductVarianceAggregatesAsync(
            fromInclusive, toExclusive, branchScope);
        var reasonAggregates = await _stocktakeRepository.GetReasonAggregatesAsync(
            fromInclusive, toExclusive, branchScope);
        var currentStock = await _inventoryRepository.GetActiveStockRowsAsync(branchScope);

        var products = await _productRepository.GetListAsync();
        return StocktakeReconciliationReportBuilder.Build(
            days,
            fromInclusive,
            today,
            dailySales,
            productSales,
            stocktakeSummary,
            productVariances,
            reasonAggregates,
            currentStock,
            products);
    }

    private async Task<IReadOnlyCollection<Guid>?> ResolveBranchScopeAsync(Guid? branchId)
    {
        if (branchId.HasValue)
        {
            await _branchAccess.EnsureAccessAsync(
                branchId.Value,
                OperationsPermissions.Sales.ManageAll);
            await _branchAccess.EnsureAccessAsync(
                branchId.Value,
                InventoryPermissions.BranchInventory.ManageAll);
            return [branchId.Value];
        }

        var salesScope = await _branchAccess.GetScopedBranchIdsAsync(OperationsPermissions.Sales.ManageAll);
        var inventoryScope = await _branchAccess.GetScopedBranchIdsAsync(InventoryPermissions.BranchInventory.ManageAll);
        if (salesScope is null) return inventoryScope;
        if (inventoryScope is null) return salesScope;

        var inventoryIds = new HashSet<Guid>(inventoryScope);
        var intersection = new List<Guid>();
        foreach (var id in salesScope)
        {
            if (inventoryIds.Contains(id)) intersection.Add(id);
        }
        return intersection;
    }

    private static int NormalizeDays(int days)
        => days <= 7 ? 7 : days <= 30 ? 30 : 90;
}
