using Microsoft.Extensions.Options;
using VpnControlPlane.Contracts.Nodes;
using VpnNodeAgent.Abstractions;
using VpnNodeAgent.Configuration;

namespace VpnNodeAgent.Services;

public sealed class AgentSnapshotService(
    IWireGuardCommandRunner commandRunner,
    IWireGuardDumpParser dumpParser,
    IConfigFileCatalog configFileCatalog,
    IWireGuardConfigParser configParser,
    IOptions<AgentOptions> options) : IAgentSnapshotService
{
    public async Task<NodeSnapshotResponse> BuildSnapshotAsync(CancellationToken cancellationToken)
    {
        var rawDump = await commandRunner.ExecuteShowAllDumpAsync(cancellationToken);
        var runtimes = dumpParser.Parse(rawDump);
        var configFiles = await configFileCatalog.ListConfigFilesAsync(cancellationToken);
        var peerConfigs = await configParser.ParseAsync(configFiles, cancellationToken);
        var configByPublicKey = peerConfigs.ToDictionary(x => x.PublicKey, StringComparer.OrdinalIgnoreCase);

        var sessions = runtimes
            .Select(runtime =>
            {
                configByPublicKey.TryGetValue(runtime.PublicKey, out var peerConfig);

                return new AgentSessionSnapshot(
                    runtime.PublicKey,
                    peerConfig?.UserExternalId,
                    peerConfig?.UserEmail,
                    peerConfig?.UserDisplayName,
                    peerConfig?.Protocol ?? "wireguard",
                    runtime.InterfaceName,
                    runtime.Endpoint,
                    runtime.LatestHandshakeAtUtc,
                    runtime.RxBytes,
                    runtime.TxBytes,
                    runtime.IsActive);
            })
            .ToList();

        return new NodeSnapshotResponse(
            options.Value.AgentIdentifier,
            options.Value.Hostname,
            options.Value.AgentVersion,
            DateTimeOffset.UtcNow,
            sessions,
            peerConfigs);
    }
}
