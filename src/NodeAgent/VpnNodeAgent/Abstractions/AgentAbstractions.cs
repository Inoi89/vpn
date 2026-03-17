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

public interface IConfigFileReader
{
    Task<IReadOnlyList<string>> ReadAllLinesAsync(string filePath, CancellationToken cancellationToken);
}

public interface IConfigFileWriter
{
    Task WriteAllTextAsync(string filePath, string contents, CancellationToken cancellationToken);
}

public interface IWireGuardConfigParser
{
    Task<IReadOnlyList<PeerConfigSnapshot>> ParseAsync(IReadOnlyList<string> configFiles, CancellationToken cancellationToken);
}

public interface IAgentSnapshotService
{
    Task<NodeSnapshotResponse> BuildSnapshotAsync(CancellationToken cancellationToken);
}

public interface IAgentAccessService
{
    Task<IssueAccessResponse> IssueAsync(IssueAccessRequest request, CancellationToken cancellationToken);

    Task<SetAccessStateResponse> SetStateAsync(SetAccessStateRequest request, CancellationToken cancellationToken);
}
