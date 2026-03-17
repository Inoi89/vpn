namespace VpnControlPlane.Infrastructure.Services;

public sealed class AgentClientOptions
{
    public const string SectionName = "AgentClient";

    public string SnapshotPath { get; set; } = "/v1/agent/snapshot";

    public string? ClientCertificatePath { get; set; }

    public string? ClientCertificatePassword { get; set; }
}
