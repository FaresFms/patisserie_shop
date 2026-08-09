using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Operations.StockTransfers;
using Production.BranchRequests;
using Production.Entities;
using Production.Kitchens;
using Production.Orders;
using Production.Permissions;
using Volo.Abp;
using Volo.Abp.Localization;

namespace Production.Dispatch;

[Authorize(ProductionPermissions.Dispatch.Default)]
public class ProductionDispatchAppService : ProductionAppService, IProductionDispatchAppService
{
    private readonly IProductionOrderRepository _orderRepository;
    private readonly IStockTransferAppService _stockTransferAppService;
    private readonly KitchenAccessChecker _kitchenAccessChecker;
    private readonly IBranchProductionRequestRepository _requestRepository;
    private readonly ProductionStockDispatchManager _stockDispatchManager;

    public ProductionDispatchAppService(
        IProductionOrderRepository orderRepository,
        IStockTransferAppService stockTransferAppService,
        KitchenAccessChecker kitchenAccessChecker,
        IBranchProductionRequestRepository requestRepository,
        ProductionStockDispatchManager stockDispatchManager)
    {
        _orderRepository = orderRepository;
        _stockTransferAppService = stockTransferAppService;
        _kitchenAccessChecker = kitchenAccessChecker;
        _requestRepository = requestRepository;
        _stockDispatchManager = stockDispatchManager;
    }

    public async Task<List<ProductionStockDispatchQueueItemDto>> GetStockDispatchQueueAsync(
        GetProductionStockDispatchQueueInput input)
    {
        await _kitchenAccessChecker.EnsureAccessAsync(input.KitchenBranchId);
        var rows = await _requestRepository.GetStockDispatchTargetsAsync(
            input.KitchenBranchId,
            Clock.Now.ToUniversalTime().Date,
            input.Filter);
        var result = new List<ProductionStockDispatchQueueItemDto>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(new ProductionStockDispatchQueueItemDto
            {
                RequestId = row.RequestId,
                RequestItemId = row.RequestItemId,
                RequestNumber = row.RequestNumber,
                BranchId = row.BranchId,
                BranchName = row.BranchName,
                NeededByDate = row.NeededByDate,
                Priority = row.Priority,
                ProductId = row.ProductId,
                ProductName = row.ProductName,
                ProductSku = row.ProductSku,
                Unit = row.Unit,
                ApprovedQuantity = row.ApprovedQuantity,
                PlannedQuantity = row.PlannedQuantity,
                FulfilledQuantity = row.FulfilledQuantity,
                RemainingUnplannedQuantity = row.RemainingUnplannedQuantity,
                UsableKitchenStock = row.UsableKitchenStock,
                CommittedKitchenStock = row.CommittedKitchenStock,
                AvailableUncommittedKitchenStock = row.AvailableUncommittedKitchenStock,
                DispatchableQuantity = row.DispatchableQuantity
            });
        }

        return result;
    }

    [Authorize(ProductionPermissions.Dispatch.CreateTransfer)]
    public async Task<ProductionDispatchResultDto> CreateTransferAsync(CreateProductionDispatchTransferDto input)
    {
        using var contentCulture = CultureHelper.Use("ar-SY", "ar-SY");

        if (input.Quantity <= 0)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("Quantity", input.Quantity);
        }

        var order = await _orderRepository.GetWithDetailsAsync(input.ProductionOrderId);
        await _kitchenAccessChecker.EnsureAccessAsync(order.KitchenBranchId);
        if (order.Status != ProductionOrderStatuses.Completed)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderStatusTransition)
                .WithData("CurrentStatus", order.Status)
                .WithData("TargetStatus", ProductionOrderStatuses.Completed);
        }
        var destinationRemaining = order.GetRemainingToDispatch(input.DestinationBranchId);
        if (input.Quantity > order.RemainingToDispatch)
        {
            throw new BusinessException(ProductionErrorCodes.DispatchQuantityExceedsRemaining)
                .WithData("Quantity", input.Quantity)
                .WithData("RemainingToDispatch", order.RemainingToDispatch);
        }
        if (input.Quantity > destinationRemaining)
        {
            throw new BusinessException(ProductionErrorCodes.DispatchDestinationHasNoAllocation)
                .WithData("DestinationBranchId", input.DestinationBranchId)
                .WithData("Quantity", input.Quantity)
                .WithData("DestinationRemaining", destinationRemaining);
        }

        var notes = L["DispatchTransferNote", order.OrderNumber].Value;
        if (!string.IsNullOrWhiteSpace(input.Notes))
        {
            notes = $"{notes} {input.Notes.Trim()}";
        }

        var transfer = await _stockTransferAppService.CreateAsync(new CreateStockTransferDto
        {
            FromBranchId = order.KitchenBranchId,
            ToBranchId = input.DestinationBranchId,
            RequestedDate = Clock.Now.Date,
            Notes = notes
        });

        await _stockTransferAppService.AddItemAsync(transfer.Id, new AddStockTransferItemDto
        {
            ProductId = order.FinishedProductId,
            RequestedQuantity = input.Quantity
        });

        // Ship, don't complete: stock leaves the kitchen here (TransferOut) and the
        // transfer rides the normal workflow — the DESTINATION branch confirms what
        // actually arrived. Completing from the kitchen would both bypass that
        // receipt check and fail authorization for a real kitchen manager, who
        // doesn't manage the destination branch.
        transfer = await _stockTransferAppService.SubmitAsync(transfer.Id);
        transfer = await _stockTransferAppService.ApproveAsync(transfer.Id);
        // Empty Lines → ship every item at its approved quantity (the full dispatch).
        transfer = await _stockTransferAppService.ShipAsync(transfer.Id, new ShipStockTransferDto());

        order.CreateDispatch(
            GuidGenerator.Create(),
            transfer.Id,
            input.DestinationBranchId,
            input.Quantity,
            Clock.Now.ToUniversalTime(),
            GuidGenerator.Create);
        await _orderRepository.UpdateAsync(order, autoSave: true);

        return new ProductionDispatchResultDto
        {
            StockTransferId = transfer.Id,
            StockTransferReference = transfer.Reference,
            DispatchedQuantity = input.Quantity,
            FulfilledRequestQuantity = 0,
            InTransitQuantity = order.InTransitQuantity,
            RemainingToDispatch = order.RemainingToDispatch
        };
    }

    [Authorize(ProductionPermissions.Dispatch.CreateTransfer)]
    public async Task<ProductionDispatchResultDto> CreateRequestStockTransferAsync(
        CreateProductionRequestStockTransferDto input)
    {
        using var contentCulture = CultureHelper.Use("ar-SY", "ar-SY");
        await _kitchenAccessChecker.EnsureAccessAsync(input.KitchenBranchId);

        var request = await _requestRepository.GetWithItemsAsync(input.RequestId);
        var reservation = await _stockDispatchManager.ReserveAsync(
            request,
            input.RequestItemId,
            input.KitchenBranchId,
            input.Quantity,
            Clock.Now.ToUniversalTime().Date);

        var notes = L["StockDispatchTransferNote", reservation.RequestNumber].Value;
        if (!string.IsNullOrWhiteSpace(input.Notes))
        {
            notes = $"{notes} {input.Notes.Trim()}";
        }

        var createTransfer = new CreateStockTransferDto
        {
            FromBranchId = input.KitchenBranchId,
            ToBranchId = reservation.DestinationBranchId,
            RequestedDate = Clock.Now.Date,
            Notes = notes
        };
        createTransfer.SetSourceDocument(
            ProductionTransferSourceTypes.BranchRequest,
            reservation.RequestId,
            reservation.RequestItemId);

        var transfer = await _stockTransferAppService.CreateAsync(createTransfer);
        await _stockTransferAppService.AddItemAsync(transfer.Id, new AddStockTransferItemDto
        {
            ProductId = reservation.ProductId,
            RequestedQuantity = reservation.Quantity
        });
        transfer = await _stockTransferAppService.SubmitAsync(transfer.Id);
        transfer = await _stockTransferAppService.ApproveAsync(transfer.Id);
        transfer = await _stockTransferAppService.ShipAsync(transfer.Id, new ShipStockTransferDto());

        await _requestRepository.UpdateAsync(request, autoSave: true);
        return new ProductionDispatchResultDto
        {
            StockTransferId = transfer.Id,
            StockTransferReference = transfer.Reference,
            DispatchedQuantity = reservation.Quantity,
            FulfilledRequestQuantity = 0,
            InTransitQuantity = reservation.Quantity,
            RemainingToDispatch = Math.Max(0, GetRemainingUnplanned(request, reservation.RequestItemId))
        };
    }

    private static int GetRemainingUnplanned(AppBranchProductionRequest request, Guid requestItemId)
    {
        foreach (var item in request.Items)
        {
            if (item.Id == requestItemId)
            {
                return item.ApprovedQuantity - item.PlannedQuantity;
            }
        }

        return 0;
    }
}
