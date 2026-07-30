using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Operations.StockTransfers;
using Production.Entities;
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

    public ProductionDispatchAppService(
        IProductionOrderRepository orderRepository,
        IStockTransferAppService stockTransferAppService)
    {
        _orderRepository = orderRepository;
        _stockTransferAppService = stockTransferAppService;
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
}
