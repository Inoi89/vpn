namespace VpnNodeAgent.Configuration;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public string AgentIdentifier { get; set; } = Environment.MachineName.ToLowerInvariant();

    public string Hostname { get; set; } = Environment.MachineName;

    public string AgentVersion { get; set; } = "1.0.0";

    public string WgExecutablePath { get; set; } = "wg";

    public string ConfigDirectory { get; set; } = "/etc/wireguard";

    public List<string> ConfigSearchPatterns { get; set; } = ["*.conf"];

    public int ActiveHandshakeWindowSeconds { get; set; } = 180;

    public List<string> AllowedClientThumbprints { get; set; } = [];
}
