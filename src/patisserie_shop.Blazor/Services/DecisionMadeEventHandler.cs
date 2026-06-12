using System.Threading.Tasks;
using Intelligence.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace patisserie_shop.Blazor.Services;

/// <summary>
/// Receives <see cref="DecisionMadeEto"/> from the distributed event bus
/// (the default local in-process bus in this solution) and forwards it to the
/// <see cref="DecisionNotificationBridge"/> so connected circuits can react.
/// </summary>
public class DecisionMadeEventHandler : IDistributedEventHandler<DecisionMadeEto>, ITransientDependency
{
    private readonly DecisionNotificationBridge _bridge;

    public DecisionMadeEventHandler(DecisionNotificationBridge bridge)
    {
        _bridge = bridge;
    }

    public Task HandleEventAsync(DecisionMadeEto eventData)
    {
        _bridge.Publish(new DecisionNotification(
            eventData.DecisionLogId,
            eventData.DecisionType,
            eventData.ProductId,
            eventData.BranchId));

        return Task.CompletedTask;
    }
}
