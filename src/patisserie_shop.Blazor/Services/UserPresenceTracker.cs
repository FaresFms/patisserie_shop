using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;

namespace patisserie_shop.Blazor.Services;

/// <summary>
/// Tracks authenticated users that currently have at least one connected
/// Blazor Server circuit. A short grace period prevents transient SignalR
/// reconnects from making the UI flicker between online and offline.
/// </summary>
public sealed class UserPresenceTracker : ISingletonDependency
{
    private static readonly TimeSpan DisconnectGracePeriod = TimeSpan.FromSeconds(10);

    private readonly object _sync = new();
    private readonly Dictionary<string, Guid> _connections = [];
    private readonly Dictionary<string, CancellationTokenSource> _pendingDisconnects = [];

    public event Action<Guid, bool>? PresenceChanged;

    public IReadOnlySet<Guid> GetOnlineUserIds()
    {
        lock (_sync)
        {
            return _connections.Values.ToHashSet();
        }
    }

    public void ConnectionUp(Guid userId, string circuitId)
    {
        var becameOnline = false;

        lock (_sync)
        {
            CancelPendingDisconnect(circuitId);

            var wasOnline = IsOnlineUnsafe(userId);
            _connections[circuitId] = userId;
            becameOnline = !wasOnline;
        }

        if (becameOnline)
        {
            PublishPresenceChanged(userId, true);
        }
    }

    public void ConnectionDown(string circuitId)
    {
        CancellationTokenSource cancellation;

        lock (_sync)
        {
            if (!_connections.ContainsKey(circuitId))
            {
                return;
            }

            CancelPendingDisconnect(circuitId);
            cancellation = new CancellationTokenSource();
            _pendingDisconnects[circuitId] = cancellation;
        }

        _ = RemoveAfterGracePeriodAsync(circuitId, cancellation);
    }

    public void CircuitClosed(string circuitId)
    {
        Guid? userWhoWentOffline = null;

        lock (_sync)
        {
            CancelPendingDisconnect(circuitId);

            if (_connections.Remove(circuitId, out var userId) &&
                !IsOnlineUnsafe(userId))
            {
                userWhoWentOffline = userId;
            }
        }

        if (userWhoWentOffline.HasValue)
        {
            PublishPresenceChanged(userWhoWentOffline.Value, false);
        }
    }

    private async Task RemoveAfterGracePeriodAsync(
        string circuitId,
        CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(DisconnectGracePeriod, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Guid? userWhoWentOffline = null;

        lock (_sync)
        {
            if (!_pendingDisconnects.TryGetValue(circuitId, out var current) ||
                !ReferenceEquals(current, cancellation))
            {
                return;
            }

            _pendingDisconnects.Remove(circuitId);
            cancellation.Dispose();

            if (_connections.Remove(circuitId, out var userId) &&
                !IsOnlineUnsafe(userId))
            {
                userWhoWentOffline = userId;
            }
        }

        if (userWhoWentOffline.HasValue)
        {
            PublishPresenceChanged(userWhoWentOffline.Value, false);
        }
    }

    private bool IsOnlineUnsafe(Guid userId)
    {
        return _connections.Values.Contains(userId);
    }

    private void CancelPendingDisconnect(string circuitId)
    {
        if (!_pendingDisconnects.Remove(circuitId, out var cancellation))
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void PublishPresenceChanged(Guid userId, bool isOnline)
    {
        var handlers = PresenceChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<Guid, bool>)handler)(userId, isOnline);
            }
            catch
            {
                // Presence is best-effort; a disposed circuit must not prevent
                // the remaining admin pages from receiving the update.
            }
        }
    }
}

/// <summary>
/// Connects the authenticated Blazor circuit lifecycle to
/// <see cref="UserPresenceTracker"/>.
/// </summary>
public sealed class UserPresenceCircuitHandler : CircuitHandler
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly UserPresenceTracker _presenceTracker;

    public UserPresenceCircuitHandler(
        AuthenticationStateProvider authenticationStateProvider,
        UserPresenceTracker presenceTracker)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _presenceTracker = presenceTracker;
    }

    public override async Task OnConnectionUpAsync(
        Circuit circuit,
        CancellationToken cancellationToken)
    {
        var authenticationState =
            await _authenticationStateProvider.GetAuthenticationStateAsync();
        var userIdValue = authenticationState.User.FindFirst(AbpClaimTypes.UserId)?.Value
                          ?? authenticationState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(userIdValue, out var userId))
        {
            _presenceTracker.ConnectionUp(userId, circuit.Id);
        }
    }

    public override Task OnConnectionDownAsync(
        Circuit circuit,
        CancellationToken cancellationToken)
    {
        _presenceTracker.ConnectionDown(circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(
        Circuit circuit,
        CancellationToken cancellationToken)
    {
        _presenceTracker.CircuitClosed(circuit.Id);
        return Task.CompletedTask;
    }
}
