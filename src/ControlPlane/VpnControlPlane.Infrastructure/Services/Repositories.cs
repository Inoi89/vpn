using Microsoft.EntityFrameworkCore;
using VpnControlPlane.Application;
using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Contracts.Nodes;
using VpnControlPlane.Domain.Entities;
using VpnControlPlane.Domain.Enums;
using VpnControlPlane.Infrastructure.Persistence;

namespace VpnControlPlane.Infrastructure.Services;

internal sealed class EfNodeRepository(ControlPlaneDbContext dbContext) : INodeRepository
{
    public Task AddAsync(Node node, CancellationToken cancellationToken)
    {
        return dbContext.Nodes.AddAsync(node, cancellationToken).AsTask();
    }

    public Task<Node?> GetByIdAsync(Guid id, bool includeRelated, CancellationToken cancellationToken)
    {
        IQueryable<Node> query = dbContext.Nodes;
        if (includeRelated)
        {
            query = query
                .Include(x => x.PeerConfigs)
                .ThenInclude(x => x.User)
                .Include(x => x.Sessions);
        }

        return query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Node?> GetByAgentIdentifierAsync(string agentIdentifier, CancellationToken cancellationToken)
    {
        return dbContext.Nodes.FirstOrDefaultAsync(x => x.AgentIdentifier == agentIdentifier, cancellationToken);
    }

    public Task<IReadOnlyList<Node>> ListAsync(bool enabledOnly, CancellationToken cancellationToken)
    {
        IQueryable<Node> query = dbContext.Nodes.AsNoTracking();
        if (enabledOnly)
        {
            query = query.Where(x => x.IsEnabled);
        }

        return query.OrderBy(x => x.Name).ToListAsync(cancellationToken).ContinueWith(static x => (IReadOnlyList<Node>)x.Result, cancellationToken);
    }
}

internal sealed class EfUserRepository(ControlPlaneDbContext dbContext) : IUserRepository
{
    public Task AddAsync(VpnUser user, CancellationToken cancellationToken)
    {
        return dbContext.Users.AddAsync(user, cancellationToken).AsTask();
    }

    public Task<VpnUser?> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken)
    {
        return dbContext.Users
            .Include(x => x.PeerConfigs)
            .FirstOrDefaultAsync(x => x.ExternalId == externalId, cancellationToken);
    }

    public Task<VpnUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return dbContext.Users
            .Include(x => x.PeerConfigs)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
    }
}

internal sealed class DashboardReadService(ControlPlaneDbContext dbContext) : IDashboardReadService
{
    public async Task<IReadOnlyList<NodeSummaryDto>> GetNodesAsync(CancellationToken cancellationToken)
    {
        var nodes = await dbContext.Nodes
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.AgentIdentifier,
                x.Name,
                x.Cluster,
                x.AgentBaseAddress,
                Status = x.Status.ToString(),
                x.AgentVersion,
                x.LastSeenAtUtc,
                ActiveSessions = x.Sessions.Count(y => y.State == SessionState.Active),
                x.LastError
            })
            .ToListAsync(cancellationToken);

        var enabledPeerCounts = await dbContext.PeerConfigs
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .GroupBy(x => x.NodeId)
            .Select(x => new { NodeId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.NodeId, x => x.Count, cancellationToken);

        var items = nodes
            .Select(x => new NodeSummaryDto(
                x.Id,
                x.AgentIdentifier,
                x.Name,
                x.Cluster,
                x.AgentBaseAddress,
                x.Status,
                x.AgentVersion,
                x.LastSeenAtUtc,
                x.ActiveSessions,
                enabledPeerCounts.GetValueOrDefault(x.Id, 0),
                x.LastError))
            .ToList();

        return items;
    }

    public async Task<IReadOnlyList<SessionDto>> GetActiveSessionsAsync(Guid? nodeId, CancellationToken cancellationToken)
    {
        var query = dbContext.Sessions
            .AsNoTracking()
            .Where(x => x.State == SessionState.Active);

        if (nodeId.HasValue)
        {
            query = query.Where(x => x.NodeId == nodeId.Value);
        }

        var items = await query
            .OrderByDescending(x => x.LastObservedAtUtc)
            .Select(x => new SessionDto(
                x.Id,
                x.NodeId,
                x.UserId,
                x.Node.Name,
                x.User.DisplayName,
                x.PeerPublicKey,
                x.Endpoint,
                x.ConnectedAtUtc,
                x.LastHandshakeAtUtc,
                x.LastRxBytes,
                x.LastTxBytes,
                x.State.ToString()))
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(x => new
            {
                x.Id,
                x.ExternalId,
                x.DisplayName,
                x.Email,
                x.IsEnabled,
                PeerCount = x.PeerConfigs.Count
            })
            .ToListAsync(cancellationToken);

        var nodeLinks = await dbContext.PeerConfigs
            .AsNoTracking()
            .Select(x => new { x.UserId, x.NodeId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var lastActivities = await dbContext.Sessions
            .AsNoTracking()
            .GroupBy(x => x.UserId)
            .Select(x => new
            {
                UserId = x.Key,
                LastActivityAtUtc = x.Max(y => (DateTimeOffset?)(y.LastHandshakeAtUtc ?? y.LastObservedAtUtc))
            })
            .ToListAsync(cancellationToken);

        var nodeIdsByUser = nodeLinks
            .GroupBy(x => x.UserId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<Guid>)x.Select(y => y.NodeId).ToArray());

        var enabledNodeLinks = await dbContext.PeerConfigs
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .Select(x => new { x.UserId, x.NodeId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var enabledNodeIdsByUser = enabledNodeLinks
            .GroupBy(x => x.UserId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<Guid>)x.Select(y => y.NodeId).ToArray());

        var lastActivityByUser = lastActivities.ToDictionary(x => x.UserId, x => x.LastActivityAtUtc);

        var items = users
            .Select(x =>
            {
                var nodeIds = nodeIdsByUser.TryGetValue(x.Id, out var foundNodeIds) ? foundNodeIds : [];
                var enabledNodeIds = enabledNodeIdsByUser.TryGetValue(x.Id, out var foundEnabledNodeIds)
                    ? foundEnabledNodeIds
                    : [];
                var effectiveIsEnabled = enabledNodeIds.Count > 0 || (nodeIds.Count == 0 && x.IsEnabled);

                return new UserSummaryDto(
                    x.Id,
                    x.ExternalId,
                    x.DisplayName,
                    x.Email,
                    effectiveIsEnabled,
                    x.PeerCount,
                    nodeIds,
                    enabledNodeIds,
                    lastActivityByUser.TryGetValue(x.Id, out var lastActivityAtUtc) ? lastActivityAtUtc : null);
            })
            .ToList();

        return items;
    }

    public async Task<IReadOnlyList<TrafficPointDto>> GetTrafficPointsAsync(int take, CancellationToken cancellationToken)
    {
        var items = await dbContext.TrafficStats
            .AsNoTracking()
            .OrderByDescending(x => x.CapturedAtUtc)
            .Take(take)
            .Select(x => new TrafficPointDto(
                x.CapturedAtUtc,
                x.User.DisplayName,
                x.RxBytes,
                x.TxBytes))
            .ToListAsync(cancellationToken);

        items.Reverse();
        return items;
    }
}

internal sealed class EfNodeSnapshotWriter(ControlPlaneDbContext dbContext, IClock clock) : INodeSnapshotWriter
{
    public async Task ApplySnapshotAsync(Node node, NodeSnapshotResponse snapshot, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var usersByExternalId = new Dictionary<string, VpnUser>(StringComparer.OrdinalIgnoreCase);
        var usersByEmail = new Dictionary<string, VpnUser>(StringComparer.OrdinalIgnoreCase);
        var peerConfigsByKey = node.PeerConfigs.ToDictionary(x => x.PublicKey, StringComparer.OrdinalIgnoreCase);
        var sessionsByKey = node.Sessions.ToDictionary(x => x.PeerPublicKey, StringComparer.OrdinalIgnoreCase);
        var observedPublicKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var observedPeerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configSnapshot in snapshot.PeerConfigs)
        {
            observedPeerKeys.Add(configSnapshot.PublicKey);
            var user = await ResolveUserAsync(
                configSnapshot.UserExternalId,
                configSnapshot.UserEmail,
                configSnapshot.UserDisplayName,
                usersByExternalId,
                usersByEmail,
                cancellationToken);

            if (!peerConfigsByKey.TryGetValue(configSnapshot.PublicKey, out var peerConfig))
            {
                peerConfig = PeerConfig.Create(
                    Guid.NewGuid(),
                    node.Id,
                    user.Id,
                    configSnapshot.UserDisplayName ?? user.DisplayName,
                    configSnapshot.PublicKey,
                    ToProtocolFlavor(configSnapshot.Protocol),
                    string.Join(",", configSnapshot.AllowedIps),
                    configSnapshot.MetadataJson,
                    configSnapshot.Revision,
                    snapshot.CollectedAtUtc,
                    now);

                await dbContext.PeerConfigs.AddAsync(peerConfig, cancellationToken);
                peerConfigsByKey[peerConfig.PublicKey] = peerConfig;
            }
            else
            {
                peerConfig.Refresh(
                    user.Id,
                    configSnapshot.UserDisplayName ?? user.DisplayName,
                    ToProtocolFlavor(configSnapshot.Protocol),
                    string.Join(",", configSnapshot.AllowedIps),
                    configSnapshot.MetadataJson,
                    configSnapshot.Revision,
                    snapshot.CollectedAtUtc,
                    now);
            }
        }

        foreach (var sessionSnapshot in snapshot.Sessions)
        {
            observedPublicKeys.Add(sessionSnapshot.PublicKey);

            if (!peerConfigsByKey.TryGetValue(sessionSnapshot.PublicKey, out var peerConfig))
            {
                var fallbackUser = await ResolveUserAsync(
                    sessionSnapshot.UserExternalId,
                    sessionSnapshot.UserEmail,
                    sessionSnapshot.UserDisplayName,
                    usersByExternalId,
                    usersByEmail,
                    cancellationToken);

                peerConfig = PeerConfig.Create(
                    Guid.NewGuid(),
                    node.Id,
                    fallbackUser.Id,
                    sessionSnapshot.UserDisplayName ?? fallbackUser.DisplayName,
                    sessionSnapshot.PublicKey,
                    ToProtocolFlavor(sessionSnapshot.Protocol),
                    string.Empty,
                    null,
                    revision: 0,
                    snapshot.CollectedAtUtc,
                    now);

                await dbContext.PeerConfigs.AddAsync(peerConfig, cancellationToken);
                peerConfigsByKey[peerConfig.PublicKey] = peerConfig;
            }

            var user = await ResolveUserAsync(
                sessionSnapshot.UserExternalId,
                sessionSnapshot.UserEmail,
                sessionSnapshot.UserDisplayName ?? peerConfig.DisplayName,
                usersByExternalId,
                usersByEmail,
                cancellationToken);

            if (!sessionsByKey.TryGetValue(sessionSnapshot.PublicKey, out var session))
            {
                session = Session.Start(
                    Guid.NewGuid(),
                    node.Id,
                    user.Id,
                    peerConfig.Id,
                    sessionSnapshot.PublicKey,
                    sessionSnapshot.Endpoint,
                    sessionSnapshot.LatestHandshakeAtUtc,
                    snapshot.CollectedAtUtc,
                    sessionSnapshot.RxBytes,
                    sessionSnapshot.TxBytes,
                    now);

                await dbContext.Sessions.AddAsync(session, cancellationToken);
                sessionsByKey[session.PeerPublicKey] = session;
            }
            else
            {
                session.Observe(
                    user.Id,
                    peerConfig.Id,
                    sessionSnapshot.Endpoint,
                    sessionSnapshot.LatestHandshakeAtUtc,
                    sessionSnapshot.IsActive,
                    snapshot.CollectedAtUtc,
                    sessionSnapshot.RxBytes,
                    sessionSnapshot.TxBytes,
                    now);
            }

            await dbContext.TrafficStats.AddAsync(
                TrafficStats.Capture(
                    Guid.NewGuid(),
                    node.Id,
                    user.Id,
                    session.Id,
                    peerConfig.Id,
                    sessionSnapshot.RxBytes,
                    sessionSnapshot.TxBytes,
                    snapshot.CollectedAtUtc,
                    now),
                cancellationToken);
        }

        foreach (var missingSession in node.Sessions.Where(x => !observedPublicKeys.Contains(x.PeerPublicKey) && x.State == SessionState.Active))
        {
            missingSession.Disconnect(snapshot.CollectedAtUtc, now);
        }

        foreach (var missingPeer in node.PeerConfigs.Where(x => !observedPeerKeys.Contains(x.PublicKey) && x.IsEnabled))
        {
            missingPeer.Disable(snapshot.CollectedAtUtc, now);
        }
    }

    private async Task<VpnUser> ResolveUserAsync(
        string? externalId,
        string? email,
        string? displayName,
        IDictionary<string, VpnUser> usersByExternalId,
        IDictionary<string, VpnUser> usersByEmail,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(externalId) && usersByExternalId.TryGetValue(externalId, out var existingByExternalId))
        {
            return existingByExternalId;
        }

        if (!string.IsNullOrWhiteSpace(email) && usersByEmail.TryGetValue(email, out var existingByEmail))
        {
            return existingByEmail;
        }

        VpnUser? user = null;

        if (!string.IsNullOrWhiteSpace(externalId))
        {
            user = await dbContext.Users.FirstOrDefaultAsync(x => x.ExternalId == externalId, cancellationToken);
        }

        if (user is null && !string.IsNullOrWhiteSpace(email))
        {
            user = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        }

        if (user is null)
        {
            var stableExternalId = string.IsNullOrWhiteSpace(externalId)
                ? email ?? $"auto-{Guid.NewGuid():N}"
                : externalId;

            var stableDisplayName = string.IsNullOrWhiteSpace(displayName)
                ? email ?? stableExternalId
                : displayName;

            user = VpnUser.Create(Guid.NewGuid(), stableExternalId, stableDisplayName, email, isEnabled: true, clock.UtcNow);
            await dbContext.Users.AddAsync(user, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(externalId))
        {
            usersByExternalId[externalId] = user;
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            usersByEmail[user.Email] = user;
        }

        return user;
    }

    private static ProtocolFlavor ToProtocolFlavor(string protocol)
    {
        return protocol.Trim().ToLowerInvariant() switch
        {
            "wireguard" => ProtocolFlavor.WireGuard,
            "amnezia" => ProtocolFlavor.AmneziaWireGuard,
            "amnezia-wireguard" => ProtocolFlavor.AmneziaWireGuard,
            _ => ProtocolFlavor.Unknown
        };
    }
}
