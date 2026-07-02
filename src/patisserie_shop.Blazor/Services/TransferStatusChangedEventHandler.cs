using System.Threading.Tasks;
using Operations.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace patisserie_shop.Blazor.Services;

/// <summary>
/// Receives <see cref="TransferStatusChangedEto"/> from the distributed event bus
/// (the default local in-process bus in this solution) and forwards it to the
/// <see cref="TransferNotificationBridge"/> so connected circuits can refresh
/// their "transfers waiting for you" counters and toast the party whose turn it is.
/// </summary>
public class TransferStatusChangedEventHandler
    : IDistributedEventHandler<TransferStatusChangedEto>, ITransientDependency
{
    private readonly TransferNotificationBridge _bridge;

    public TransferStatusChangedEventHandler(TransferNotificationBridge bridge)
    {
        _bridge = bridge;
    }

    public Task HandleEventAsync(TransferStatusChangedEto eventData)
    {
        _bridge.Publish(new TransferNotification(
            eventData.TransferId,
            eventData.FromBranchId,
            eventData.ToBranchId,
            eventData.OldStatus,
            eventData.NewStatus));

        return Task.CompletedTask;
    }
}
