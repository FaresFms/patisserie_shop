using System;
using System.Threading.Tasks;
using Intelligence.Services;
using Inventory.Events;
using Inventory.StockBatches;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Timing;

namespace Intelligence.EventHandlers;

public class StockChangedEventHandler :
    IDistributedEventHandler<StockChangedEto>,
    ITransientDependency
{
    private readonly DecisionMakerService _decisionMakerService;
    private readonly IStockBatchRepository _batchRepository;
    private readonly IClock _clock;

    public StockChangedEventHandler(
        DecisionMakerService decisionMakerService,
        IStockBatchRepository batchRepository,
        IClock clock)
    {
        _decisionMakerService = decisionMakerService;
        _batchRepository = batchRepository;
        _clock = clock;
    }

    public async Task HandleEventAsync(StockChangedEto eventData)
    {
        // Expired units on the shelf are not sellable — a branch whose stock is
        // entirely expired must still trigger low-stock/stockout rules. The batch
        // ledger is best-effort, so the expired figure is advisory and clamped
        // inside the evaluation.
        var expiredQuantity = await _batchRepository.GetExpiredQuantityAsync(
            eventData.BranchId, eventData.ProductId, _clock.Now.ToUniversalTime().Date);

        await _decisionMakerService.EvaluateAsync(eventData, expiredQuantity);
    }
}
