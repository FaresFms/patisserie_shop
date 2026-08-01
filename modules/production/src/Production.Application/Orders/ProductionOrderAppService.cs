using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Settings;
using Microsoft.AspNetCore.Authorization;
using Operations.PurchaseOrders;
using Production.Entities;
using Production.Permissions;
using Production.Plans;
using Production.Waste;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Localization;
using Volo.Abp.Settings;

namespace Production.Orders;

[Authorize(ProductionPermissions.Orders.Default)]
public class ProductionOrderAppService : ProductionAppService, IProductionOrderAppService
{
    private const string ReferenceType = nameof(AppProductionOrder);

    private readonly IProductionOrderRepository _orderRepository;
    private readonly ProductionOrderManager _orderManager;
    private readonly IProductionPlanRepository _planRepository;
    private readonly ProductionPlanManager _planManager;
    private readonly IBranchInventoryRepository _branchInventoryRepository;
    private readonly BranchInventoryManager _inventoryManager;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IPurchaseOrderAppService _purchaseOrderAppService;
    private readonly ProductionWasteManager _wasteManager;
    private readonly IProductionWasteRepository _wasteRepository;
    private readonly ISettingProvider _settingProvider;

    public ProductionOrderAppService(
        IProductionOrderRepository orderRepository,
        ProductionOrderManager orderManager,
        IProductionPlanRepository planRepository,
        ProductionPlanManager planManager,
        IBranchInventoryRepository branchInventoryRepository,
        BranchInventoryManager inventoryManager,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        IPurchaseOrderAppService purchaseOrderAppService,
        ProductionWasteManager wasteManager,
        IProductionWasteRepository wasteRepository,
        ISettingProvider settingProvider)
    {
        _orderRepository = orderRepository;
        _orderManager = orderManager;
        _planRepository = planRepository;
        _planManager = planManager;
        _branchInventoryRepository = branchInventoryRepository;
        _inventoryManager = inventoryManager;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _purchaseOrderAppService = purchaseOrderAppService;
        _wasteManager = wasteManager;
        _wasteRepository = wasteRepository;
        _settingProvider = settingProvider;
    }

    public async Task<ProductionOrderDto> GetAsync(Guid id)
    {
        var order = await _orderRepository.GetWithDetailsAsync(id);
        return await MapToDtoAsync(order);
    }

    public async Task<PagedResultDto<ProductionOrderListItemDto>> GetListAsync(GetProductionOrdersInput input)
    {
        var totalCount = await _orderRepository.CountFilteredAsync(
            input.Filter, input.Status, input.KitchenBranchId);

        var items = await _orderRepository.GetFilteredListAsync(
            input.Filter,
            input.Status,
            input.KitchenBranchId,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        var dtos = new List<ProductionOrderListItemDto>();
        foreach (var item in items)
        {
            dtos.Add(new ProductionOrderListItemDto
            {
                Id = item.Id,
                OrderNumber = item.OrderNumber,
                KitchenBranchId = item.KitchenBranchId,
                KitchenBranchName = item.KitchenBranchName,
                FinishedProductId = item.FinishedProductId,
                FinishedProductName = item.FinishedProductName,
                FinishedProductSku = item.FinishedProductSku,
                Unit = item.Unit,
                Status = item.Status,
                Priority = item.Priority,
                PlannedOutputQuantity = item.PlannedOutputQuantity,
                AcceptedQuantity = item.AcceptedQuantity,
                ReservedQuantity = item.ReservedQuantity,
                DispatchedQuantity = item.DispatchedQuantity,
                InTransitQuantity = item.InTransitQuantity,
                ReceivedQuantity = item.ReceivedQuantity,
                LostQuantity = item.LostQuantity,
                RemainingToDispatch = item.RemainingToDispatch,
                ActualStartTime = item.ActualStartTime,
                CompletedAt = item.CompletedAt,
                TotalProductionCost = item.TotalProductionCost
            });
        }

        return new PagedResultDto<ProductionOrderListItemDto>(totalCount, dtos);
    }

    [Authorize(ProductionPermissions.Orders.Create)]
    public async Task<List<ProductionOrderDto>> CreateFromPlanAsync(Guid planId)
    {
        var plan = await _planRepository.GetWithLinesAsync(planId);
        var orders = await _orderManager.CreateFromPlanAsync(plan, CurrentUser.Id);

        var dtos = new List<ProductionOrderDto>();
        foreach (var order in orders)
        {
            await _orderRepository.InsertAsync(order);
            dtos.Add(await MapToDtoAsync(order));
        }

        return dtos;
    }

    public async Task<ProductionOrderDto> RefreshAvailabilityAsync(Guid id)
    {
        var order = await _orderRepository.GetWithDetailsAsync(id);
        await _orderManager.RefreshAvailabilityAsync(order);
        await _orderRepository.UpdateAsync(order, autoSave: true);
        return await MapToDtoAsync(order);
    }

    [Authorize(ProductionPermissions.Ingredients.CheckAvailability)]
    public async Task<CreateIngredientPurchaseOrdersResultDto> CreateDraftIngredientPurchaseOrdersAsync(Guid id)
    {
        using var contentCulture = CultureHelper.Use("ar-SY", "ar-SY");
        var order = await _orderRepository.GetWithDetailsAsync(id);
        var availability = await _orderManager.CheckAvailabilityAsync(order);
        var shortages = availability
            .Where(a => a.HasShortage && a.ShortageQuantity > 0)
            .ToList();

        var result = new CreateIngredientPurchaseOrdersResultDto
        {
            TotalShortageQuantity = shortages.Sum(s => s.ShortageQuantity)
        };
        if (shortages.Count == 0)
        {
            return result;
        }

        var productIds = shortages.Select(s => s.IngredientProductId).Distinct().ToList();
        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id));
        var productById = products.ToDictionary(p => p.Id);

        foreach (var shortage in shortages)
        {
            if (!productById.TryGetValue(shortage.IngredientProductId, out var product)
                || !product.DefaultSupplierId.HasValue)
            {
                throw new BusinessException(ProductionErrorCodes.IngredientDefaultSupplierRequired)
                    .WithData("IngredientProductId", shortage.IngredientProductId)
                    .WithData("IngredientName", shortage.IngredientName);
            }
        }

        var currency = await GetDefaultCurrencyAsync();

        foreach (var group in shortages.GroupBy(s => productById[s.IngredientProductId].DefaultSupplierId!.Value))
        {
            var po = await _purchaseOrderAppService.CreateAsync(new CreatePurchaseOrderDto
            {
                SupplierId = group.Key,
                DestBranchId = order.KitchenBranchId,
                OrderDate = Clock.Now.Date,
                ExpectedDeliveryDate = Clock.Now.Date.AddDays(1),
                Currency = currency,
                Notes = L["IngredientPurchaseOrderNote", order.OrderNumber]
            });

            foreach (var shortage in group)
            {
                var product = productById[shortage.IngredientProductId];
                await _purchaseOrderAppService.AddItemAsync(po.Id, new AddPurchaseOrderItemDto
                {
                    ProductId = product.Id,
                    OrderedQuantity = shortage.ShortageQuantity,
                    UnitPrice = product.CostPrice
                });
            }

            po = await _purchaseOrderAppService.GetAsync(po.Id);
            result.PurchaseOrders.Add(new IngredientPurchaseOrderDto
            {
                PurchaseOrderId = po.Id,
                PONumber = po.PONumber,
                SupplierName = po.SupplierName,
                LineCount = po.Items.Count
            });
        }

        return result;
    }

    [Authorize(ProductionPermissions.Orders.Start)]
    public async Task<ProductionOrderDto> StartAsync(Guid id)
    {
        var order = await _orderRepository.GetWithDetailsAsync(id);
        await _orderManager.RefreshAvailabilityAsync(order);

        if (order.Status == ProductionOrderStatuses.WaitingForIngredients)
        {
            await _orderRepository.UpdateAsync(order, autoSave: true);
            throw new BusinessException(ProductionErrorCodes.IngredientShortage)
                .WithData("ProductionOrderId", order.Id);
        }

        var consumptionLines = order.Start(CurrentUser.Id);
        foreach (var line in consumptionLines)
        {
            var inventoryRow = await _branchInventoryRepository.FindByBranchAndProductAsync(
                order.KitchenBranchId,
                line.IngredientProductId);

            var inventory = inventoryRow?.Inventory;
            if (inventory == null || inventory.QuantityOnHand < line.Quantity)
            {
                throw new BusinessException(ProductionErrorCodes.IngredientShortage)
                    .WithData("IngredientProductId", line.IngredientProductId)
                    .WithData("Required", line.Quantity)
                    .WithData("Available", inventory?.QuantityOnHand ?? 0);
            }

            await _inventoryManager.AdjustStockAsync(
                inventory,
                inventory.QuantityOnHand - line.Quantity,
                StockMovementTypes.ProductionConsumption,
                notes: order.OrderNumber,
                referenceId: order.Id,
                referenceType: ReferenceType);

            await _branchInventoryRepository.UpdateAsync(inventory);
        }

        await _orderRepository.UpdateAsync(order, autoSave: true);
        await RefreshPlanLifecycleAsync(order.ProductionPlanId);
        return await MapToDtoAsync(order);
    }

    [Authorize(ProductionPermissions.Orders.Complete)]
    public async Task<ProductionOrderDto> CompleteAsync(Guid id, CompleteProductionOrderDto input)
    {
        var order = await _orderRepository.GetWithDetailsAsync(id);
        var finishedProduct = await _productRepository.GetAsync(order.FinishedProductId);
        var expiryDate = ResolveExpiryDate(input.ExpiryDate, finishedProduct, order);
        var completionNotes = NormalizeSystemNotesForPersistence(order, input.Notes);

        var completion = order.Complete(
            input.ActualOutputQuantity,
            input.AcceptedQuantity,
            input.RejectedQuantity,
            expiryDate,
            input.WasteReason,
            CurrentUser.Id,
            completionNotes);
        var output = completion.Output;
        await _orderManager.ApplyAllocationReleasesAsync(completion.ReleasedAllocations);

        var inventory = await _branchInventoryRepository.FindByBranchAndProductAsync(
            order.KitchenBranchId,
            output.ProductId);

        var finishedInventory = inventory?.Inventory;
        if (finishedInventory == null)
        {
            finishedInventory = await _inventoryManager.InitializeAsync(order.KitchenBranchId, output.ProductId);
            await _branchInventoryRepository.InsertAsync(finishedInventory);
        }

        await _inventoryManager.AdjustStockAsync(
            finishedInventory,
            finishedInventory.QuantityOnHand + output.AcceptedQuantity,
            StockMovementTypes.ProductionOutput,
            notes: order.OrderNumber,
            referenceId: order.Id,
            referenceType: ReferenceType,
            batchExpiryDate: output.ExpiryDate);

        await _branchInventoryRepository.UpdateAsync(finishedInventory);

        if (input.RejectedQuantity > 0)
        {
            var wasteReason = ResolveRejectedWasteReason(input.WasteReason);
            var rejectedUnitCost = input.ActualOutputQuantity > 0
                ? Math.Round(order.TotalProductionCost / input.ActualOutputQuantity, 4)
                : order.UnitProductionCost;
            string? wasteNotes;
            using (CultureHelper.Use("ar-SY", "ar-SY"))
            {
                wasteNotes = string.IsNullOrWhiteSpace(input.WasteReason) ||
                             ProductionWasteReasons.IsValid(input.WasteReason)
                    ? completionNotes
                    : AppendNote(completionNotes, L["OriginalWasteReason", input.WasteReason.Trim()]);
            }

            var waste = await _wasteManager.CreateAsync(
                productionOrderId: order.Id,
                kitchenBranchId: order.KitchenBranchId,
                productId: order.FinishedProductId,
                wasteType: ProductionWasteTypes.RejectedOutput,
                quantity: input.RejectedQuantity,
                unitCost: rejectedUnitCost,
                reason: wasteReason,
                recordedByUserId: CurrentUser.Id,
                recordedAt: Clock.Now,
                notes: wasteNotes);

            await _wasteRepository.InsertAsync(waste);
        }

        await _orderRepository.UpdateAsync(order, autoSave: true);
        await RefreshPlanLifecycleAsync(order.ProductionPlanId);

        return await MapToDtoAsync(order);
    }

    [Authorize(ProductionPermissions.Orders.Cancel)]
    public async Task<ProductionOrderDto> CancelAsync(Guid id)
    {
        var order = await _orderRepository.GetWithDetailsAsync(id);
        var releases = order.Cancel();
        await _orderManager.ApplyAllocationReleasesAsync(releases);
        await _orderRepository.UpdateAsync(order, autoSave: true);
        await RefreshPlanLifecycleAsync(order.ProductionPlanId);
        return await MapToDtoAsync(order);
    }

    private DateTime ResolveExpiryDate(DateTime? requestedExpiryDate, AppProduct product, AppProductionOrder order)
    {
        if (requestedExpiryDate.HasValue)
        {
            return requestedExpiryDate.Value.Date;
        }

        if (product.ShelfLifeDays.HasValue)
        {
            // Shelf life counts from when cooking actually started, not from when
            // the completion was recorded — completing an order the next morning
            // must not stretch the product's life by a day.
            var cookedDate = order.ActualStartTime?.Date ?? Clock.Now.Date;
            return cookedDate.AddDays(product.ShelfLifeDays.Value);
        }

        throw new BusinessException(ProductionErrorCodes.ProductionExpiryDateRequired)
            .WithData("ProductId", product.Id);
    }

    private static string ResolveRejectedWasteReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return ProductionWasteReasons.Other;
        }

        var trimmed = reason.Trim();
        return ProductionWasteReasons.IsValid(trimmed)
            ? trimmed
            : ProductionWasteReasons.Other;
    }

    private async Task<ProductionOrderDto> MapToDtoAsync(AppProductionOrder order)
    {
        var dto = new ProductionOrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            KitchenBranchId = order.KitchenBranchId,
            ProductionPlanId = order.ProductionPlanId,
            ProductionPlanLineId = order.ProductionPlanLineId,
            FinishedProductId = order.FinishedProductId,
            FormulaId = order.FormulaId,
            FormulaVersion = order.FormulaVersion,
            Status = order.Status,
            Priority = order.Priority,
            PlannedOutputQuantity = order.PlannedOutputQuantity,
            ActualOutputQuantity = order.ActualOutputQuantity,
            AcceptedQuantity = order.AcceptedQuantity,
            RejectedQuantity = order.RejectedQuantity,
            ReservedQuantity = order.ReservedQuantity,
            DispatchedQuantity = order.DispatchedQuantity,
            InTransitQuantity = order.InTransitQuantity,
            ReceivedQuantity = order.ReceivedQuantity,
            LostQuantity = order.LostQuantity,
            RemainingToDispatch = order.RemainingToDispatch,
            PlannedStartTime = order.PlannedStartTime,
            ActualStartTime = order.ActualStartTime,
            CompletedAt = order.CompletedAt,
            ExpiryDate = order.ExpiryDate,
            PlannedIngredientCost = order.PlannedIngredientCost,
            ActualIngredientCost = order.ActualIngredientCost,
            LaborCost = order.LaborCost,
            OverheadCost = order.OverheadCost,
            TotalProductionCost = order.TotalProductionCost,
            UnitProductionCost = order.UnitProductionCost,
            WasteReason = order.WasteReason,
            Notes = FriendlySystemNotes(order.Notes, order.ProductionPlanId.HasValue)
        };

        var branch = await _branchRepository.FindAsync(order.KitchenBranchId);
        dto.KitchenBranchName = branch?.Name;

        var productIds = new List<Guid> { order.FinishedProductId };
        foreach (var ingredient in order.Ingredients)
        {
            productIds.Add(ingredient.IngredientProductId);
        }

        foreach (var allocation in order.Allocations)
        {
            dto.Allocations.Add(new ProductionOrderAllocationDto
            {
                Id = allocation.Id,
                BranchId = allocation.BranchId,
                BranchProductionRequestId = allocation.BranchProductionRequestId,
                BranchProductionRequestItemId = allocation.BranchProductionRequestItemId,
                AllocatedQuantity = allocation.AllocatedQuantity,
                DispatchedQuantity = allocation.DispatchedQuantity,
                InTransitQuantity = allocation.InTransitQuantity,
                ReceivedQuantity = allocation.ReceivedQuantity,
                LostQuantity = allocation.LostQuantity,
                RemainingToDispatch = allocation.RemainingToDispatch
            });
        }

        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id));
        var productById = new Dictionary<Guid, AppProduct>();
        foreach (var product in products)
        {
            productById[product.Id] = product;
        }

        if (productById.TryGetValue(order.FinishedProductId, out var finishedProduct))
        {
            dto.FinishedProductName = finishedProduct.Name;
            dto.FinishedProductSku = finishedProduct.SKU;
            dto.FinishedProductUnit = finishedProduct.Unit;
        }

        foreach (var ingredient in order.Ingredients)
        {
            productById.TryGetValue(ingredient.IngredientProductId, out var ingredientProduct);
            dto.Ingredients.Add(new ProductionOrderIngredientDto
            {
                Id = ingredient.Id,
                IngredientProductId = ingredient.IngredientProductId,
                IngredientName = ingredientProduct?.Name,
                IngredientSku = ingredientProduct?.SKU,
                Unit = ingredientProduct?.Unit,
                RequiredQuantity = ingredient.RequiredQuantity,
                ConsumedQuantity = ingredient.ConsumedQuantity,
                UnitCostSnapshot = ingredient.UnitCostSnapshot,
                TotalCost = ingredient.TotalCost
            });
        }

        var availability = await _orderManager.CheckAvailabilityAsync(order);
        foreach (var line in availability)
        {
            dto.Availability.Add(new ProductionIngredientAvailabilityDto
            {
                IngredientProductId = line.IngredientProductId,
                IngredientName = line.IngredientName,
                IngredientSku = line.IngredientSku,
                Unit = line.Unit,
                RequiredQuantity = line.RequiredQuantity,
                AvailableQuantity = line.AvailableQuantity,
                ShortageQuantity = line.ShortageQuantity,
                HasShortage = line.HasShortage
            });
        }

        return dto;
    }

    private string? FriendlySystemNotes(string? notes, bool wasCreatedFromPlan)
    {
        if (!TryGetLegacyPlanNumber(notes, wasCreatedFromPlan, out var planNumber))
        {
            return notes;
        }

        return L["ProductionOrderFromPlanNote", planNumber].Value;
    }

    private string? NormalizeSystemNotesForPersistence(
        AppProductionOrder order,
        string? submittedNotes)
    {
        if (!TryGetLegacyPlanNumber(
                order.Notes,
                order.ProductionPlanId.HasValue,
                out var planNumber))
        {
            return submittedNotes;
        }

        var callerDisplayText = L["ProductionOrderFromPlanNote", planNumber].Value;
        var noteWasNotEdited =
            string.Equals(submittedNotes, order.Notes, StringComparison.Ordinal) ||
            string.Equals(submittedNotes, callerDisplayText, StringComparison.Ordinal);
        if (!noteWasNotEdited)
        {
            return submittedNotes;
        }

        using var contentCulture = CultureHelper.Use("ar-SY", "ar-SY");
        return L["ProductionOrderFromPlanNote", planNumber].Value;
    }

    private static bool TryGetLegacyPlanNumber(
        string? notes,
        bool wasCreatedFromPlan,
        out string planNumber)
    {
        const string legacyPlanPrefix = "From plan ";

        planNumber = string.Empty;
        if (!wasCreatedFromPlan ||
            string.IsNullOrWhiteSpace(notes) ||
            !notes.StartsWith(legacyPlanPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        planNumber = notes[legacyPlanPrefix.Length..].Trim();
        return planNumber.StartsWith("PLAN-", StringComparison.Ordinal) &&
               planNumber.All(character =>
                   char.IsLetterOrDigit(character) || character == '-');
    }

    private async Task<string> GetDefaultCurrencyAsync()
        => ShopCurrencySettings.Normalize(
            await _settingProvider.GetOrNullAsync(ShopCurrencySettings.Name));

    private async Task RefreshPlanLifecycleAsync(Guid? productionPlanId)
    {
        if (!productionPlanId.HasValue)
        {
            return;
        }

        var plan = await _planRepository.GetWithLinesAsync(productionPlanId.Value);
        await _planManager.RefreshLifecycleAsync(plan);
        await _planRepository.UpdateAsync(plan, autoSave: true);
    }

    private static string AppendNote(string? notes, string extra)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return extra;
        }

        return $"{notes.Trim()} | {extra}";
    }
}
