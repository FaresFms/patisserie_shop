using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Settings;
using Inventory.StockBatches;
using Microsoft.AspNetCore.Authorization;
using Operations.PurchaseOrders;
using Production.Entities;
using Production.Control;
using Production.Kitchens;
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
    private readonly KitchenAccessChecker _kitchenAccessChecker;
    private readonly IStockBatchRepository _stockBatchRepository;

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
        ISettingProvider settingProvider,
        KitchenAccessChecker kitchenAccessChecker,
        IStockBatchRepository stockBatchRepository)
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
        _kitchenAccessChecker = kitchenAccessChecker;
        _stockBatchRepository = stockBatchRepository;
    }

    public async Task<ProductionOrderDto> GetAsync(Guid id)
    {
        var order = await _orderRepository.GetWithDetailsAsync(id);
        await _kitchenAccessChecker.EnsureAccessAsync(order.KitchenBranchId);
        return await MapToDtoAsync(order);
    }

    public async Task<PagedResultDto<ProductionOrderListItemDto>> GetListAsync(GetProductionOrdersInput input)
    {
        var kitchenIds = await GetKitchenScopeAsync(input.KitchenBranchId);
        var totalCount = await _orderRepository.CountFilteredAsync(
            input.Filter, input.Status, input.KitchenBranchId, kitchenIds);

        var items = await _orderRepository.GetFilteredListAsync(
            input.Filter,
            input.Status,
            input.KitchenBranchId,
            kitchenIds,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        var dtos = items.ConvertAll(MapListItem);
        await FillUsableKitchenStockAsync(dtos);

        return new PagedResultDto<ProductionOrderListItemDto>(totalCount, dtos);
    }

    /// <summary>
    /// Overlays how much of each finished product is actually shippable today
    /// (non-expired batches), so the dispatch screen can warn before a transfer is
    /// attempted instead of failing at the ship step. One batch query per kitchen.
    /// </summary>
    private async Task FillUsableKitchenStockAsync(List<ProductionOrderListItemDto> dtos)
    {
        if (dtos.Count == 0)
        {
            return;
        }

        var today = Clock.Now.ToUniversalTime().Date;
        var nonExpiredByKitchen = new Dictionary<Guid, Dictionary<Guid, int>>();
        foreach (var kitchenId in dtos.Select(d => d.KitchenBranchId).Distinct())
        {
            nonExpiredByKitchen[kitchenId] =
                await _stockBatchRepository.GetNonExpiredQuantitiesByProductAsync(kitchenId, today);
        }

        foreach (var dto in dtos)
        {
            dto.UsableKitchenStock =
                nonExpiredByKitchen.TryGetValue(dto.KitchenBranchId, out var byProduct)
                && byProduct.TryGetValue(dto.FinishedProductId, out var usable)
                    ? usable
                    : 0;
        }
    }

    [Authorize(ProductionPermissions.Orders.Quality)]
    public async Task<List<ProductionOrderListItemDto>> GetQualityQueueAsync(Guid? kitchenBranchId = null)
    {
        var kitchenIds = await GetKitchenScopeAsync(kitchenBranchId);
        var items = await _orderRepository.GetQualityQueueAsync(kitchenBranchId, kitchenIds);
        return items.ConvertAll(MapListItem);
    }

    [Authorize(ProductionPermissions.Orders.Create)]
    public async Task<List<ProductionOrderDto>> CreateFromPlanAsync(Guid planId)
    {
        var plan = await _planRepository.GetWithLinesAsync(planId);
        await _kitchenAccessChecker.EnsureAccessAsync(plan.KitchenBranchId);
        var orders = await _orderManager.CreateFromPlanAsync(plan, CurrentUser.Id);

        var dtos = new List<ProductionOrderDto>();
        foreach (var order in orders)
        {
            await _orderRepository.InsertAsync(order);
            dtos.Add(await MapToDtoAsync(order));
        }

        return dtos;
    }

    [Authorize(ProductionPermissions.Orders.Create)]
    public async Task<ProductionOrderForRequestResultDto> CreateForRequestItemAsync(
        CreateProductionOrderForRequestDto input)
    {
        await _kitchenAccessChecker.EnsureAccessAsync(input.KitchenBranchId);
        await _planManager.EnsureMainKitchenAsync(input.KitchenBranchId);

        var order = await _orderManager.CreateForRequestItemAsync(
            input.KitchenBranchId,
            input.RequestId,
            input.RequestItemId,
            input.Quantity,
            CurrentUser.Id,
            input.Notes);

        await _orderRepository.InsertAsync(order, autoSave: true);

        // One-click means one click: schedule it on the default work center/shift so the
        // baker can press Start immediately. Without this the order looks ready but
        // Start is refused by RequireScheduleBeforeStart, with nothing on screen saying
        // why. Best-effort — an unschedulable order is still a valid order.
        var autoScheduled = await TryAutoScheduleAsync(order);

        return new ProductionOrderForRequestResultDto
        {
            ProductionOrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            PlannedOutputQuantity = order.PlannedOutputQuantity,
            HasIngredientShortage = order.Status == ProductionOrderStatuses.WaitingForIngredients,
            ReadyToStart = autoScheduled && order.Status == ProductionOrderStatuses.ReadyToCook
        };
    }

    /// <summary>
    /// Assigns the first active work center, shift and operator so a one-click cook
    /// order satisfies the schedule/operator gates. Returns false when the shop has no
    /// usable work center, shift or operator configured.
    /// </summary>
    private async Task<bool> TryAutoScheduleAsync(AppProductionOrder order)
    {
        var profile = await GetControlProfileAsync();

        var workCenter = profile.WorkCenters.FirstOrDefault(center =>
            center.IsActive
            && (!center.KitchenBranchId.HasValue || center.KitchenBranchId == order.KitchenBranchId));
        var shift = profile.Shifts.FirstOrDefault(item => item.IsActive);
        if (workCenter == null || shift == null)
        {
            return false;
        }

        // Whoever pressed "cook this" is the operator — that is the whole point of the
        // one-click path, and it avoids depending on a separate operator roster.
        if (!CurrentUser.Id.HasValue)
        {
            return false;
        }

        var operatorName = CurrentUser.Name ?? CurrentUser.UserName ?? string.Empty;
        var (start, end) = NextSlot(shift, workCenter.CapacityUnitsPerHour, order.PlannedOutputQuantity);

        var used = await _orderRepository.CountOverlappingSchedulesAsync(
            order.KitchenBranchId, workCenter.Code, start, end, order.Id);
        if (used >= Math.Max(1, workCenter.ParallelSlots))
        {
            return false;
        }

        order.Schedule(workCenter.Code, shift.Code, start, end, CurrentUser.Id.Value, operatorName);
        await _orderRepository.UpdateAsync(order, autoSave: true);
        return true;
    }

    /// <summary>Earliest slot inside the shift that fits the batch, from now onward.</summary>
    private (DateTime Start, DateTime End) NextSlot(
        ProductionShiftDto shift,
        int capacityUnitsPerHour,
        int quantity)
    {
        var now = Clock.Now;
        var start = now.Date.Add(shift.StartTime);
        var shiftEnd = now.Date.Add(shift.EndTime);
        if (shiftEnd <= start)
        {
            shiftEnd = shiftEnd.AddDays(1);
        }

        if (now > shiftEnd)
        {
            start = start.AddDays(1);
        }
        else if (now > start)
        {
            start = now;
        }

        var perHour = Math.Max(1, capacityUnitsPerHour);
        var minutes = Math.Max(15, (int)Math.Ceiling(quantity / (decimal)perHour * 60m));
        return (start, start.AddMinutes(minutes));
    }

    public async Task<ProductionOrderDto> RefreshAvailabilityAsync(Guid id)
    {
        var order = await _orderRepository.GetWithDetailsAsync(id);
        await _kitchenAccessChecker.EnsureAccessAsync(order.KitchenBranchId);
        await _orderManager.RefreshAvailabilityAsync(order);
        await _orderRepository.UpdateAsync(order, autoSave: true);
        return await MapToDtoAsync(order);
    }

    [Authorize(ProductionPermissions.Orders.Schedule)]
    public async Task<ProductionOrderDto> ScheduleAsync(
        Guid id,
        ScheduleProductionOrderDto input)
    {
        var order = await _orderRepository.GetWithDetailsAsync(id);
        await _kitchenAccessChecker.EnsureAccessAsync(order.KitchenBranchId);

        var profile = await GetControlProfileAsync();
        var workCenter = profile.WorkCenters.FirstOrDefault(center =>
            center.IsActive
            && string.Equals(center.Code, input.WorkCenterCode, StringComparison.OrdinalIgnoreCase)
            && (!center.KitchenBranchId.HasValue || center.KitchenBranchId == order.KitchenBranchId));
        var shift = profile.Shifts.FirstOrDefault(item =>
            item.IsActive
            && string.Equals(item.Code, input.ShiftCode, StringComparison.OrdinalIgnoreCase));
        if (workCenter == null || shift == null)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidProductionSchedule);
        }

        _orderManager.EnsureScheduleDuration(
            order,
            workCenter.CapacityUnitsPerHour,
            input.ScheduledStartTime,
            input.ScheduledEndTime);
        var overlappingSchedules = await _orderRepository.CountOverlappingSchedulesAsync(
            order.KitchenBranchId,
            workCenter.Code,
            input.ScheduledStartTime,
            input.ScheduledEndTime,
            order.Id);
        if (overlappingSchedules >= Math.Max(1, workCenter.ParallelSlots))
        {
            throw new BusinessException(ProductionErrorCodes.ProductionCapacityExceeded)
                .WithData("WorkCenterCode", workCenter.Code)
                .WithData("ParallelSlots", workCenter.ParallelSlots);
        }

        order.Schedule(
            workCenter.Code,
            shift.Code,
            input.ScheduledStartTime,
            input.ScheduledEndTime,
            input.OperatorUserId,
            input.OperatorName);
        await _orderRepository.UpdateAsync(order, autoSave: true);
        return await MapToDtoAsync(order);
    }

    [Authorize(ProductionPermissions.Orders.Create)]
    public async Task<CreateSubProductionOrdersResultDto> CreateSubProductionOrdersAsync(Guid id)
    {
        var parent = await _orderRepository.GetWithDetailsAsync(id);
        await _kitchenAccessChecker.EnsureAccessAsync(parent.KitchenBranchId);
        var children = await _orderManager.CreateSubProductionOrdersAsync(parent, CurrentUser.Id);
        var result = new CreateSubProductionOrdersResultDto();
        foreach (var child in children)
        {
            await _orderRepository.InsertAsync(child);
            result.Orders.Add(await MapToDtoAsync(child));
        }
        return result;
    }

    [Authorize(ProductionPermissions.Ingredients.CheckAvailability)]
    public async Task<CreateIngredientPurchaseOrdersResultDto> CreateDraftIngredientPurchaseOrdersAsync(Guid id)
    {
        using var contentCulture = CultureHelper.Use("ar-SY", "ar-SY");
        var order = await _orderRepository.GetWithDetailsAsync(id);
        await _kitchenAccessChecker.EnsureAccessAsync(order.KitchenBranchId);
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
        await _kitchenAccessChecker.EnsureAccessAsync(order.KitchenBranchId);
        await _orderManager.RefreshAvailabilityAsync(order);

        if (order.Status == ProductionOrderStatuses.WaitingForIngredients)
        {
            await _orderRepository.UpdateAsync(order, autoSave: true);
            throw new BusinessException(ProductionErrorCodes.IngredientShortage)
                .WithData("ProductionOrderId", order.Id);
        }

        var controlProfile = await GetControlProfileAsync();
        order.EnsureReadyForStart(
            controlProfile.RequireScheduleBeforeStart,
            controlProfile.RequireOperatorBeforeStart);

        // Planned cost remains the historical plan snapshot. Refresh the ingredient
        // prices immediately before consumption so ActualIngredientCost reflects the
        // current source cost at the moment cooking starts.
        await _orderManager.RefreshIngredientCostSnapshotsAsync(order);
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

            var adjustment = await _inventoryManager.AdjustStockDetailedAsync(
                inventory,
                inventory.QuantityOnHand - line.Quantity,
                StockMovementTypes.ProductionConsumption,
                notes: order.OrderNumber,
                referenceId: order.Id,
                referenceType: ReferenceType);

            _orderManager.RecordActualIngredientCost(order, line, adjustment.ConsumedBatches);

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
        await _kitchenAccessChecker.EnsureAccessAsync(order.KitchenBranchId);
        var finishedProduct = await _productRepository.GetAsync(order.FinishedProductId);
        var expiryDate = input.AcceptedQuantity > 0
            ? ProductionExpiryPolicy.Resolve(
                input.ExpiryDate,
                finishedProduct.ShelfLifeDays,
                order.ActualStartTime ?? Clock.Now,
                finishedProduct.Id)
            : (DateTime?)null;
        var completionNotes = NormalizeSystemNotesForPersistence(order, input.Notes);

        var completion = order.Complete(
            input.ActualOutputQuantity,
            input.AcceptedQuantity,
            input.RejectedQuantity,
            expiryDate,
            input.WasteReason,
            CurrentUser.Id,
            completionNotes);
        var controlProfile = await GetControlProfileAsync();
        if (!controlProfile.RequireQualityReleaseBeforeDispatch && input.AcceptedQuantity > 0)
        {
            order.SkipQualityRelease(CurrentUser.Id, Clock.Now);
        }
        var output = completion.Output;
        await _orderManager.ApplyAllocationReleasesAsync(completion.ReleasedAllocations);

        if (output.AcceptedQuantity > 0)
        {
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
                batchExpiryDate: output.ExpiryDate,
                batchUnitCost: output.UnitCost);

            await _branchInventoryRepository.UpdateAsync(finishedInventory, autoSave: true);

            var outputBatches = await _stockBatchRepository.GetBySourceAsync(
                StockBatchSourceTypes.ProductionOutput,
                order.Id);
            var outputBatch = outputBatches.LastOrDefault(batch =>
                batch.BranchId == order.KitchenBranchId
                && batch.ProductId == output.ProductId);
            if (outputBatch != null)
            {
                order.RecordOutputBatch(outputBatch.Id, outputBatch.BatchNumber);
            }
        }

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

    [Authorize(ProductionPermissions.Orders.Quality)]
    public async Task<ProductionOrderDto> HoldQualityAsync(
        Guid id,
        ProductionQualityActionDto input)
    {
        var order = await _orderRepository.GetWithDetailsAsync(id);
        await _kitchenAccessChecker.EnsureAccessAsync(order.KitchenBranchId);
        order.HoldQuality(input.Reason ?? string.Empty, CurrentUser.Id, Clock.Now);
        await _orderRepository.UpdateAsync(order, autoSave: true);
        return await MapToDtoAsync(order);
    }

    [Authorize(ProductionPermissions.Orders.Quality)]
    public async Task<ProductionOrderDto> ReleaseQualityAsync(
        Guid id,
        ProductionQualityActionDto input)
    {
        var order = await _orderRepository.GetWithDetailsAsync(id);
        await _kitchenAccessChecker.EnsureAccessAsync(order.KitchenBranchId);
        order.ReleaseQuality(input.Reason, CurrentUser.Id, Clock.Now);
        await _orderRepository.UpdateAsync(order, autoSave: true);
        return await MapToDtoAsync(order);
    }

    [Authorize(ProductionPermissions.Orders.Quality)]
    public async Task<ProductionOrderDto> RejectQualityAsync(
        Guid id,
        ProductionQualityActionDto input)
    {
        var order = await _orderRepository.GetWithDetailsAsync(id);
        await _kitchenAccessChecker.EnsureAccessAsync(order.KitchenBranchId);
        order.RejectQuality(input.Reason ?? string.Empty, CurrentUser.Id, Clock.Now);

        if (order.AcceptedQuantity > 0)
        {
            var inventoryRow = await _branchInventoryRepository.FindByBranchAndProductAsync(
                order.KitchenBranchId,
                order.FinishedProductId);
            var inventory = inventoryRow?.Inventory;
            if (inventory == null || inventory.QuantityOnHand < order.AcceptedQuantity)
            {
                throw new BusinessException(ProductionErrorCodes.WasteQuantityExceedsStock)
                    .WithData("Requested", order.AcceptedQuantity)
                    .WithData("Available", inventory?.QuantityOnHand ?? 0);
            }

            await _inventoryManager.AdjustStockAsync(
                inventory,
                inventory.QuantityOnHand - order.AcceptedQuantity,
                StockMovementTypes.ProductionWaste,
                notes: order.OrderNumber,
                referenceId: order.Id,
                referenceType: ReferenceType);
            await _branchInventoryRepository.UpdateAsync(inventory);

            var waste = await _wasteManager.CreateAsync(
                order.Id,
                order.KitchenBranchId,
                order.FinishedProductId,
                ProductionWasteTypes.RejectedOutput,
                order.AcceptedQuantity,
                order.UnitProductionCost,
                ProductionWasteReasons.Other,
                CurrentUser.Id,
                Clock.Now,
                input.Reason);
            await _wasteRepository.InsertAsync(waste);
        }

        await _orderRepository.UpdateAsync(order, autoSave: true);
        return await MapToDtoAsync(order);
    }

    [Authorize(ProductionPermissions.Orders.Cancel)]
    public async Task<ProductionOrderDto> CancelAsync(Guid id)
    {
        var order = await _orderRepository.GetWithDetailsAsync(id);
        await _kitchenAccessChecker.EnsureAccessAsync(order.KitchenBranchId);
        var releases = order.Cancel();
        await _orderManager.ApplyAllocationReleasesAsync(releases);
        await _orderRepository.UpdateAsync(order, autoSave: true);
        await RefreshPlanLifecycleAsync(order.ProductionPlanId);
        return await MapToDtoAsync(order);
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

    private async Task<List<Guid>> GetKitchenScopeAsync(Guid? kitchenBranchId)
    {
        if (kitchenBranchId.HasValue)
        {
            await _kitchenAccessChecker.EnsureAccessAsync(kitchenBranchId.Value);
        }

        return await _kitchenAccessChecker.GetAccessibleKitchenIdsAsync();
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
            Notes = FriendlySystemNotes(order.Notes, order.ProductionPlanId.HasValue),
            ParentProductionOrderId = order.ParentProductionOrderId,
            WorkCenterCode = order.WorkCenterCode,
            ShiftCode = order.ShiftCode,
            AssignedOperatorUserId = order.AssignedOperatorUserId,
            AssignedOperatorName = order.AssignedOperatorName,
            ScheduledStartTime = order.ScheduledStartTime,
            ScheduledEndTime = order.ScheduledEndTime,
            FormulaAllergens = order.FormulaAllergens,
            QualityStatus = order.QualityStatus,
            QualityReason = order.QualityReason,
            QualityUpdatedAt = order.QualityUpdatedAt,
            QualityUpdatedByUserId = order.QualityUpdatedByUserId,
            OutputBatchId = order.OutputBatchId,
            OutputBatchNumber = order.OutputBatchNumber
        };

        var branch = await _branchRepository.FindAsync(order.KitchenBranchId);
        dto.KitchenBranchName = branch?.DisplayName;

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
            dto.FinishedProductName = finishedProduct.DisplayName;
            dto.FinishedProductSku = finishedProduct.SKU;
            dto.FinishedProductUnit = finishedProduct.DisplayUnit;
        }

        foreach (var ingredient in order.Ingredients)
        {
            productById.TryGetValue(ingredient.IngredientProductId, out var ingredientProduct);
            dto.Ingredients.Add(new ProductionOrderIngredientDto
            {
                Id = ingredient.Id,
                IngredientProductId = ingredient.IngredientProductId,
                IngredientName = ingredientProduct?.DisplayName,
                IngredientSku = ingredientProduct?.SKU,
                Unit = ingredientProduct?.DisplayUnit,
                RequiredQuantity = ingredient.RequiredQuantity,
                ConsumedQuantity = ingredient.ConsumedQuantity,
                UnitCostSnapshot = ingredient.UnitCostSnapshot,
                TotalCost = ingredient.TotalCost
            });
        }

        foreach (var lot in order.GetIngredientLots())
        {
            productById.TryGetValue(lot.IngredientProductId, out var ingredientProduct);
            dto.IngredientLots.Add(new ProductionIngredientLotDto
            {
                IngredientProductId = lot.IngredientProductId,
                IngredientName = ingredientProduct?.DisplayName,
                BatchId = lot.BatchId,
                BatchNumber = lot.BatchNumber,
                ExpiryDate = lot.ExpiryDate,
                Quantity = lot.Quantity,
                UnitCost = lot.UnitCost
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

    private static ProductionOrderListItemDto MapListItem(ProductionOrderListItem item) => new()
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
        TotalProductionCost = item.TotalProductionCost,
        QualityStatus = item.QualityStatus,
        QualityReason = item.QualityReason,
        QualityUpdatedAt = item.QualityUpdatedAt,
        OutputBatchNumber = item.OutputBatchNumber,
        WorkCenterCode = item.WorkCenterCode,
        ShiftCode = item.ShiftCode,
        AssignedOperatorName = item.AssignedOperatorName,
        ScheduledStartTime = item.ScheduledStartTime,
        ScheduledEndTime = item.ScheduledEndTime
    };

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
