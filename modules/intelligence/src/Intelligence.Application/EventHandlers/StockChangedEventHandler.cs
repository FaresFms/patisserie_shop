using System.Threading.Tasks;
using Intelligence.Services;
using Inventory.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace Intelligence.EventHandlers;

public class StockChangedEventHandler :
    IDistributedEventHandler<StockChangedEto>,
    ITransientDependency
{
    private readonly DecisionMakerService _decisionMakerService;

    public StockChangedEventHandler(DecisionMakerService decisionMakerService)
    {
        _decisionMakerService = decisionMakerService;
    }

    public async Task HandleEventAsync(StockChangedEto eventData)
    {
        await _decisionMakerService.EvaluateAsync(eventData);
    }
}
