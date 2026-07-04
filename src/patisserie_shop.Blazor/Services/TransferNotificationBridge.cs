using System;
using Volo.Abp.DependencyInjection;

namespace patisserie_shop.Blazor.Services;

/// <summary>
/// Payload pushed from the in-process distributed event bus to connected
/// Blazor Server circuits whenever a stock transfer changes workflow status.
/// </summary>
public record TransferNotification(
    Guid TransferId,
    Guid? FromBranchId,
    Guid ToBranchId,
    string OldStatus,
    string NewStatus);

/// <summary>
/// In-process singleton bridge between the (local) distributed event bus and
/// the per-circuit <c>NotificationBell</c> components — same pattern as
/// <see cref="DecisionNotificationBridge"/>. <c>TransferStatusChangedEventHandler</c>
/// publishes into it; each circuit re-queries its own branch-scoped action
/// summary, so no per-user routing is needed here.
/// </summary>
public class TransferNotificationBridge : ISingletonDependency
{
    public event Action<TransferNotification>? TransferChanged;

    public void Publish(TransferNotification notification)
    {
        var handlers = TransferChanged;
        if (handlers == null)
        {
            return;
        }

        // Isolate subscribers: a faulty or half-disposed circuit must not break
        // the publishing unit of work or starve the other circuits.
        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<TransferNotification>)handler)(notification);
            }
            catch
            {
                // Notification delivery is best-effort by design.
            }
        }
    }
}
