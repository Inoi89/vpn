namespace VpnControlPlane.Infrastructure.Services;

public sealed class AgentClientOptions
{
    public const string SectionName = "AgentClient";

    public string SnapshotPath { get; set; } = "/v1/agent/snapshot";

    public string IssueAccessPath { get; set; } = "/v1/agent/accesses/issue";

    public string SetAccessStatePath { get; set; } = "/v1/agent/accesses/state";

    public string DeleteAccessPath { get; set; } = "/v1/agent/accesses/delete";

    public string GetAccessConfigPath { get; set; } = "/v1/agent/accesses/config";

    public string? ClientCertificatePath { get; set; }

    public string? ClientCertificatePassword { get; set; }

    public bool AllowInvalidServerCertificate { get; set; }
}
