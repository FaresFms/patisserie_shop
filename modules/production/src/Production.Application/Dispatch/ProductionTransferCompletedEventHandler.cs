using System.Linq;
using System.Threading.Tasks;
using Operations.Events;
using Production.BranchRequests;
using Production.Orders;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Timing;

namespace Production.Dispatch;

public class ProductionTransferCompletedEventHandler
    : IDistributedEventHandler<TransferCompletedEto>, ITransientDependency
{
    private readonly IProductionOrderRepository _orderRepository;
    private readonly ProductionOrderManager _orderManager;
    private readonly IBranchProductionRequestRepository _requestRepository;
    private readonly IClock _clock;

    public ProductionTransferCompletedEventHandler(
        IProductionOrderRepository orderRepository,
        ProductionOrderManager orderManager,
        IBranchProductionRequestRepository requestRepository,
        IClock clock)
    {
        _orderRepository = orderRepository;
        _orderManager = orderManager;
        _requestRepository = requestRepository;
        _clock = clock;
    }

    public async Task HandleEventAsync(TransferCompletedEto eventData)
    {
        var order = await _orderRepository.FindByStockTransferIdAsync(eventData.TransferId);
        if (order != null)
        {
            await CompleteOrderDispatchAsync(order, eventData);
            return;
        }

        if (eventData.SourceDocumentType == ProductionTransferSourceTypes.BranchRequest)
        {
            await CompleteRequestStockDispatchAsync(eventData);
        }
    }

    private async Task CompleteOrderDispatchAsync(
        Production.Entities.AppProductionOrder order,
        TransferCompletedEto eventData)
    {

        var productLines = eventData.Lines
            .Where(x => x.ProductId == order.FinishedProductId)
            .ToList();
        if (productLines.Count == 0)
        {
            throw new BusinessException(ProductionErrorCodes.ProductionDispatchNotFound)
                .WithData("StockTransferId", eventData.TransferId)
                .WithData("ProductId", order.FinishedProductId);
        }

        var receivedQuantity = productLines.Sum(x => x.ReceivedQuantity);
        var results = order.CompleteDispatch(
            eventData.TransferId,
            receivedQuantity,
            _clock.Now.ToUniversalTime());
        if (results.Count == 0)
        {
            return;
        }

        await _orderManager.ApplyDispatchResultsAsync(results);
        await _orderRepository.UpdateAsync(order, autoSave: true);
    }

    private async Task CompleteRequestStockDispatchAsync(TransferCompletedEto eventData)
    {
        if (!eventData.SourceDocumentId.HasValue || !eventData.SourceDocumentItemId.HasValue)
        {
            throw new BusinessException(ProductionErrorCodes.StockDispatchReconciliationMismatch)
                .WithData("StockTransferId", eventData.TransferId);
        }

        var request = await _requestRepository.GetWithItemsAsync(eventData.SourceDocumentId.Value);
        if (request.BranchId != eventData.ToBranchId || eventData.Lines.Count != 1)
        {
            throw new BusinessException(ProductionErrorCodes.StockDispatchReconciliationMismatch)
                .WithData("StockTransferId", eventData.TransferId)
                .WithData("DestinationBranchId", eventData.ToBranchId);
        }

        var line = eventData.Lines[0];
        request.CompleteReservedStockDispatch(
            eventData.SourceDocumentItemId.Value,
            line.ProductId,
            line.ShippedQuantity,
            line.ReceivedQuantity);
        await _requestRepository.UpdateAsync(request, autoSave: true);
    }
}
