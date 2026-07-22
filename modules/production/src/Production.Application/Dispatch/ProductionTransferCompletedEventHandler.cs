using System.Linq;
using System.Threading.Tasks;
using Operations.Events;
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
    private readonly IClock _clock;

    public ProductionTransferCompletedEventHandler(
        IProductionOrderRepository orderRepository,
        ProductionOrderManager orderManager,
        IClock clock)
    {
        _orderRepository = orderRepository;
        _orderManager = orderManager;
        _clock = clock;
    }

    public async Task HandleEventAsync(TransferCompletedEto eventData)
    {
        var order = await _orderRepository.FindByStockTransferIdAsync(eventData.TransferId);
        if (order == null)
        {
            return;
        }

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
}
