namespace VpnNodeAgent.Configuration;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public string AgentIdentifier { get; set; } = Environment.MachineName.ToLowerInvariant();

    public string Hostname { get; set; } = Environment.MachineName;

    public string AgentVersion { get; set; } = "1.0.0";

    public string OperationMode { get; set; } = "Host";

    public string WgExecutablePath { get; set; } = "wg";

    public string WgQuickExecutablePath { get; set; } = "wg-quick";

    public string DockerExecutablePath { get; set; } = "docker";

    public string? DockerContainerName { get; set; }

    public string ConfigDirectory { get; set; } = "/etc/wireguard";

    public List<string> ConfigSearchPatterns { get; set; } = ["*.conf", "clientsTable"];

    public int ActiveHandshakeWindowSeconds { get; set; } = 180;

    public List<string> AllowedClientThumbprints { get; set; } = [];
}
