using System.Diagnostics;
using System.IO;
using VpnClient.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace VpnClient.Infrastructure.Services;

public class VpnService : IVpnService
{
    private readonly IWintunService _wintun;
    private readonly ILogger<VpnService> _logger;
    private VpnState _state = VpnState.Disconnected;

    public event Action<string>? LogReceived;

    public VpnState State => _state;

    private void LogInfo(string message)
    {
        _logger.LogInformation(message);
        LogReceived?.Invoke(message);
    }

    private void LogError(string message)
    {
        _logger.LogError(message);
        LogReceived?.Invoke(message);
    }

    public VpnService(IWintunService wintun, ILogger<VpnService> logger)
    {
        _wintun = wintun;
        _logger = logger;
    }

    public async Task ConnectAsync(string config)
    {
        if (_state != VpnState.Disconnected)
            return;

        _state = VpnState.Connecting;
        LogInfo("Creating adapter...");
        await _wintun.CreateAdapterAsync("VpnClient");

        // Parse config for address, DNS and allowed IPs
        ParseConfig(config, out var address, out var dnsServers, out var allowedIps);

        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, StripConfig(config));
        bool started = false;
        try
        {
            started = await RunWgSetConfAsync(tmp);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }

        if (!started)
        {
            _state = VpnState.Disconnected;
            LogError("wg.exe setconf failed");
            return;
        }

        // Apply IP address and DNS servers to the created adapter
        if (!string.IsNullOrWhiteSpace(address))
            await ConfigureAddressAsync(address);
        if (dnsServers.Count > 0)
            await ConfigureDnsAsync(dnsServers);

        // Add routes for allowed IPs
        foreach (var net in allowedIps)
            await AddRouteAsync(net);

        _state = VpnState.Connected;
        LogInfo("Connected");
    }

    public async Task DisconnectAsync()
    {
        if (_state != VpnState.Connected)
            return;

        _state = VpnState.Disconnecting;

        LogInfo("Removing adapter...");
        await _wintun.DeleteAdapterAsync("VpnClient");

        _state = VpnState.Disconnected;
        LogInfo("Disconnected");
    }

    private static void ParseConfig(string config, out string? address, out List<string> dns, out List<string> allowedIps)
    {
        address = null;
        dns = new();
        allowedIps = new();

        var lines = config.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Address", StringComparison.OrdinalIgnoreCase))
            {
                address = trimmed.Split('=', 2)[1].Trim();
            }
            else if (trimmed.StartsWith("DNS", StringComparison.OrdinalIgnoreCase))
            {
                var servers = trimmed.Split('=', 2)[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var s in servers)
                    dns.Add(s.Trim());
            }
            else if (trimmed.StartsWith("AllowedIPs", StringComparison.OrdinalIgnoreCase))
            {
                var nets = trimmed.Split('=', 2)[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var n in nets)
                    allowedIps.Add(n.Trim());
            }
        }
    }

    private static string StripConfig(string config)
    {
        var lines = config.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("Address", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("DNS", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("AllowedIPs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    private async Task ConfigureAddressAsync(string address)
    {
        var parts = address.Split('/');
        if (parts.Length != 2)
            return;

        var ip = parts[0];
        if (ip.Contains('.'))
        {
            if (int.TryParse(parts[1], out var prefix))
            {
                var mask = PrefixToMask(prefix);
                await RunCommandAsync("netsh", $"interface ip set address name=\"VpnClient\" static {ip} {mask}");
            }
        }
        else if (ip.Contains(':'))
        {
            await RunCommandAsync("netsh", $"interface ipv6 add address VpnClient {ip}/{parts[1]}");
        }
    }

    private async Task ConfigureDnsAsync(IReadOnlyList<string> servers)
    {
        if (servers.Count == 0) return;

        await RunCommandAsync("netsh", $"interface ip set dns name=\"VpnClient\" static {servers[0]}");
        for (int i = 1; i < servers.Count; i++)
        {
            await RunCommandAsync("netsh", $"interface ip add dns name=\"VpnClient\" {servers[i]} index={i + 1}");
        }
    }

    private async Task AddRouteAsync(string network)
    {
        var parts = network.Split('/');
        if (parts.Length != 2)
            return;

        if (parts[0].Contains('.'))
        {
            if (int.TryParse(parts[1], out var prefix))
            {
                var mask = PrefixToMask(prefix);
                await RunCommandAsync("netsh", $"interface ipv4 add route {parts[0]} {mask} VpnClient");
            }
        }
        else if (parts[0].Contains(':'))
        {
            await RunCommandAsync("netsh", $"interface ipv6 add route {parts[0]}/{parts[1]} interface=VpnClient");
        }
    }

    private static string PrefixToMask(int prefixLength)
    {
        uint mask = prefixLength == 0 ? 0 : uint.MaxValue << (32 - prefixLength);
        var bytes = BitConverter.GetBytes(mask);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return string.Join('.', bytes);
    }

    private async Task RunCommandAsync(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null)
            return;
        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        if (!string.IsNullOrWhiteSpace(output))
            LogInfo(output.Trim());
        if (!string.IsNullOrWhiteSpace(error))
            LogError(error.Trim());
    }

    private async Task<bool> RunWgSetConfAsync(string configPath)
    {
        var psi = new ProcessStartInfo("wg.exe", $"setconf VpnClient \"{configPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null)
            return false;
        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        if (!string.IsNullOrWhiteSpace(output))
            LogInfo(output.Trim());
        if (!string.IsNullOrWhiteSpace(error))
            LogError(error.Trim());
        return proc.ExitCode == 0;
    }
}
