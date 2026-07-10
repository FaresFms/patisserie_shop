using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Operations.StockTransfers;
using Production.BranchRequests;
using Production.Entities;
using Production.Orders;
using Production.Permissions;
using Volo.Abp;

namespace Production.Dispatch;

[Authorize(ProductionPermissions.Dispatch.Default)]
public class ProductionDispatchAppService : ProductionAppService, IProductionDispatchAppService
{
    private readonly IProductionOrderRepository _orderRepository;
    private readonly IBranchProductionRequestRepository _requestRepository;
    private readonly IStockTransferAppService _stockTransferAppService;

    public ProductionDispatchAppService(
        IProductionOrderRepository orderRepository,
        IBranchProductionRequestRepository requestRepository,
        IStockTransferAppService stockTransferAppService)
    {
        _orderRepository = orderRepository;
        _requestRepository = requestRepository;
        _stockTransferAppService = stockTransferAppService;
    }

    [Authorize(ProductionPermissions.Dispatch.CreateTransfer)]
    public async Task<ProductionDispatchResultDto> CreateTransferAsync(CreateProductionDispatchTransferDto input)
    {
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
        if (input.Quantity > order.AcceptedQuantity)
        {
            throw new BusinessException(ProductionErrorCodes.InvalidOrderQuantity)
                .WithData("Quantity", input.Quantity)
                .WithData("AcceptedQuantity", order.AcceptedQuantity);
        }

        var notes = $"Production dispatch from cook order {order.OrderNumber}.";
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

        var fulfilledQuantity = await ApplyRequestFulfillmentAsync(
            input.DestinationBranchId,
            order.FinishedProductId,
            input.Quantity);

        return new ProductionDispatchResultDto
        {
            StockTransferId = transfer.Id,
            StockTransferReference = transfer.Reference,
            DispatchedQuantity = input.Quantity,
            FulfilledRequestQuantity = fulfilledQuantity
        };
    }

    private async Task<int> ApplyRequestFulfillmentAsync(Guid branchId, Guid productId, int dispatchedQuantity)
    {
        var remaining = dispatchedQuantity;
        var fulfilled = 0;
        var targets = await _requestRepository.GetFulfillmentTargetsAsync(branchId, productId);

        foreach (var targetGroup in targets.GroupBy(t => t.RequestId))
        {
            if (remaining <= 0)
            {
                break;
            }

            var request = await _requestRepository.GetWithItemsAsync(targetGroup.Key);
            foreach (var target in targetGroup)
            {
                if (remaining <= 0)
                {
                    break;
                }

                var quantity = Math.Min(remaining, target.RemainingQuantity);
                request.AddFulfilledQuantity(target.RequestItemId, quantity);
                remaining -= quantity;
                fulfilled += quantity;
            }

            await _requestRepository.UpdateAsync(request);
        }

        return fulfilled;
    }
}
