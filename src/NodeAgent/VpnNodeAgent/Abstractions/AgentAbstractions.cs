using VpnControlPlane.Contracts.Nodes;
using VpnNodeAgent.Models;

namespace VpnNodeAgent.Abstractions;

public interface IWireGuardCommandRunner
{
    Task<string> ExecuteShowAllDumpAsync(CancellationToken cancellationToken);
}

public interface IWireGuardDumpParser
{
    IReadOnlyList<WireGuardPeerRuntime> Parse(string rawDump);
}

public interface IConfigFileCatalog
{
    Task<IReadOnlyList<string>> ListConfigFilesAsync(CancellationToken cancellationToken);
}

public interface IWireGuardConfigParser
{
    Task<IReadOnlyList<PeerConfigSnapshot>> ParseAsync(IReadOnlyList<string> configFiles, CancellationToken cancellationToken);
}

public interface IAgentSnapshotService
{
    Task<NodeSnapshotResponse> BuildSnapshotAsync(CancellationToken cancellationToken);
}
