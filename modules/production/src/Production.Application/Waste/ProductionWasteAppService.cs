using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inventory;
using Inventory.BranchInventory;
using Microsoft.AspNetCore.Authorization;
using Production.Entities;
using Production.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Production.Waste;

[Authorize(ProductionPermissions.Waste.Default)]
public class ProductionWasteAppService : ProductionAppService, IProductionWasteAppService
{
    private readonly IProductionWasteRepository _wasteRepository;
    private readonly ProductionWasteManager _wasteManager;
    private readonly IBranchInventoryRepository _branchInventoryRepository;
    private readonly BranchInventoryManager _inventoryManager;

    public ProductionWasteAppService(
        IProductionWasteRepository wasteRepository,
        ProductionWasteManager wasteManager,
        IBranchInventoryRepository branchInventoryRepository,
        BranchInventoryManager inventoryManager)
    {
        _wasteRepository = wasteRepository;
        _wasteManager = wasteManager;
        _branchInventoryRepository = branchInventoryRepository;
        _inventoryManager = inventoryManager;
    }

    public async Task<PagedResultDto<ProductionWasteDto>> GetListAsync(GetProductionWastesInput input)
    {
        var totalCount = await _wasteRepository.CountFilteredAsync(
            input.Filter,
            input.WasteType,
            input.KitchenBranchId,
            input.FromDate,
            input.ToDate);

        var rows = await _wasteRepository.GetFilteredListAsync(
            input.Filter,
            input.WasteType,
            input.KitchenBranchId,
            input.FromDate,
            input.ToDate,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        var dtos = new List<ProductionWasteDto>();
        foreach (var row in rows)
        {
            dtos.Add(MapToDto(row));
        }

        return new PagedResultDto<ProductionWasteDto>(totalCount, dtos);
    }

    public async Task<ProductionWasteAnalyticsDto> GetAnalyticsAsync(GetProductionWasteAnalyticsInput input)
    {
        var model = await _wasteRepository.GetAnalyticsAsync(input.Days, input.KitchenBranchId);
        var dto = new ProductionWasteAnalyticsDto
        {
            Summary = new ProductionWasteSummaryDto
            {
                TotalQuantity = model.Summary.TotalQuantity,
                TotalCost = model.Summary.TotalCost,
                TopReason = model.Summary.TopReason,
                TopProductName = model.Summary.TopProductName
            }
        };

        foreach (var point in model.DailySeries)
        {
            dto.DailySeries.Add(new ProductionWasteDailyPointDto
            {
                Date = point.Date,
                Quantity = point.Quantity,
                Cost = point.Cost
            });
        }

        foreach (var reason in model.Reasons)
        {
            dto.Reasons.Add(new ProductionWasteReasonSliceDto
            {
                Reason = reason.Reason,
                Quantity = reason.Quantity,
                Cost = reason.Cost
            });
        }

        foreach (var product in model.TopProducts)
        {
            dto.TopProducts.Add(new ProductionWasteProductRowDto
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                ProductSku = product.ProductSku,
                Quantity = product.Quantity,
                Cost = product.Cost
            });
        }

        return dto;
    }

    [Authorize(ProductionPermissions.Waste.WriteOff)]
    public async Task<ProductionWasteDto> CreateWriteOffAsync(CreateProductionWasteWriteOffDto input)
    {
        if (input.Quantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidWasteQuantity)
                .WithData("Quantity", input.Quantity);
        }

        var inventoryRow = await _branchInventoryRepository.FindByBranchAndProductAsync(
            input.KitchenBranchId,
            input.ProductId);
        if (inventoryRow?.Inventory == null)
        {
            throw new BusinessException(ProductionErrorCodes.WasteInventoryNotFound)
                .WithData("KitchenBranchId", input.KitchenBranchId)
                .WithData("ProductId", input.ProductId);
        }

        if (inventoryRow.Inventory.QuantityOnHand < input.Quantity)
        {
            throw new BusinessException(ProductionErrorCodes.WasteQuantityExceedsStock)
                .WithData("Available", inventoryRow.Inventory.QuantityOnHand)
                .WithData("Requested", input.Quantity);
        }

        var reason = ProductionWasteReasons.IsValid(input.Reason)
            ? input.Reason
            : ProductionWasteReasons.Other;
        var wasteType = ResolveWasteType(reason);

        var waste = await _wasteManager.CreateAsync(
            productionOrderId: null,
            kitchenBranchId: input.KitchenBranchId,
            productId: input.ProductId,
            wasteType: wasteType,
            quantity: input.Quantity,
            unitCost: inventoryRow.Product.CostPrice,
            reason: reason,
            recordedByUserId: CurrentUser.Id,
            recordedAt: Clock.Now,
            notes: input.Notes);

        await _wasteRepository.InsertAsync(waste);

        await _inventoryManager.AdjustStockAsync(
            inventoryRow.Inventory,
            inventoryRow.Inventory.QuantityOnHand - input.Quantity,
            StockMovementTypes.ProductionWaste,
            notes: $"هدر إنتاج: {FormatReason(reason)}",
            referenceId: waste.Id,
            referenceType: nameof(AppProductionWaste));

        await _branchInventoryRepository.UpdateAsync(inventoryRow.Inventory, autoSave: true);

        return new ProductionWasteDto
        {
            Id = waste.Id,
            ProductionOrderId = waste.ProductionOrderId,
            KitchenBranchId = waste.KitchenBranchId,
            KitchenBranchName = input.KitchenBranchId.ToString(),
            ProductId = waste.ProductId,
            ProductName = inventoryRow.Product.Name,
            ProductSku = inventoryRow.Product.SKU,
            ProductUnit = inventoryRow.Product.Unit,
            WasteType = waste.WasteType,
            Quantity = waste.Quantity,
            UnitCost = waste.UnitCost,
            TotalCost = waste.TotalCost,
            Reason = waste.Reason,
            Notes = waste.Notes,
            RecordedAt = waste.RecordedAt
        };
    }

    private static ProductionWasteDto MapToDto(ProductionWasteListItem row) =>
        new()
        {
            Id = row.Id,
            ProductionOrderId = row.ProductionOrderId,
            ProductionOrderNumber = row.ProductionOrderNumber,
            KitchenBranchId = row.KitchenBranchId,
            KitchenBranchName = row.KitchenBranchName,
            ProductId = row.ProductId,
            ProductName = row.ProductName,
            ProductSku = row.ProductSku,
            ProductUnit = row.ProductUnit,
            WasteType = row.WasteType,
            Quantity = row.Quantity,
            UnitCost = row.UnitCost,
            TotalCost = row.TotalCost,
            Reason = row.Reason,
            Notes = row.Notes,
            RecordedAt = row.RecordedAt
        };

    private static string ResolveWasteType(string reason) => reason switch
    {
        ProductionWasteReasons.ExpiredBeforeDispatch => ProductionWasteTypes.ExpiredFinishedGood,
        ProductionWasteReasons.IngredientSpoilage => ProductionWasteTypes.IngredientSpoilage,
        _ => ProductionWasteTypes.ManualWriteOff
    };

    private static string FormatReason(string reason) => reason switch
    {
        ProductionWasteReasons.Burned => "محروق",
        ProductionWasteReasons.UnderBaked => "غير ناضج",
        ProductionWasteReasons.OverBaked => "ناضج أكثر من اللازم",
        ProductionWasteReasons.ShapeDamaged => "الشكل تالف",
        ProductionWasteReasons.Dropped => "سقط أثناء التشغيل",
        ProductionWasteReasons.Contaminated => "تلوث",
        ProductionWasteReasons.IngredientSpoilage => "تلف خامات",
        ProductionWasteReasons.PackagingDamage => "تلف تغليف",
        ProductionWasteReasons.ExpiredBeforeDispatch => "انتهت الصلاحية قبل الصرف",
        ProductionWasteReasons.TestBatch => "دفعة تجربة",
        _ => "سبب آخر"
    };
}
