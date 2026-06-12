using System;
using Volo.Abp.DependencyInjection;

namespace patisserie_shop.Blazor.Services;

/// <summary>
/// Payload pushed from the in-process distributed event bus to connected
/// Blazor Server circuits when a new AppDecisionLog row is created.
/// </summary>
public record DecisionNotification(
    Guid DecisionLogId,
    string DecisionType,
    Guid ProductId,
    Guid? BranchId);

/// <summary>
/// In-process singleton bridge between the (local) distributed event bus and
/// the per-circuit <c>NotificationBell</c> components. Each circuit subscribes
/// to <see cref="DecisionMade"/>; <c>DecisionMadeEventHandler</c> publishes
/// into it. No SignalR hub is needed — the circuit itself pushes UI updates.
/// </summary>
public class DecisionNotificationBridge : ISingletonDependency
{
    public event Action<DecisionNotification>? DecisionMade;

    public void Publish(DecisionNotification notification)
    {
        var handlers = DecisionMade;
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
                ((Action<DecisionNotification>)handler)(notification);
            }
            catch
            {
                // Notification delivery is best-effort by design.
            }
        }
    }
}
