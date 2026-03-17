using Microsoft.AspNetCore.SignalR;
using VpnControlPlane.Application;
using VpnControlPlane.Application.Abstractions;

namespace VpnControlPlane.Api.Hubs;

public sealed class SignalRSessionRealtimeNotifier(IHubContext<SessionUpdatesHub> hubContext) : ISessionRealtimeNotifier
{
    public Task PublishSnapshotAsync(NodeRealtimeEnvelope snapshot, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Group("dashboard").SendAsync("sessionSnapshotUpdated", snapshot, cancellationToken);
    }
}
