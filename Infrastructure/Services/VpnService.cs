using System.Diagnostics;
using VpnClient.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace VpnClient.Infrastructure.Services;

public class VpnService : IVpnService
{
    private readonly IWintunService _wintun;
    private readonly ILogger<VpnService> _logger;
    private Process? _process;
    private VpnState _state = VpnState.Disconnected;

    public VpnState State => _state;

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
        _logger.LogInformation("Creating adapter...");
        await _wintun.CreateAdapterAsync("VpnClient");

        _logger.LogInformation("Starting WireGuard...");
        var psi = new ProcessStartInfo("wireguard-go.exe", "VpnClient")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = Process.Start(psi);
        if (_process != null)
        {
            await _process.StandardInput.WriteAsync(config);
            _process.StandardInput.Close();

            _process.OutputDataReceived += (_, e) => { if (e.Data != null) _logger.LogInformation(e.Data); };
            _process.ErrorDataReceived += (_, e) => { if (e.Data != null) _logger.LogError(e.Data); };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        _state = VpnState.Connected;
        _logger.LogInformation("Connected");
    }

    public async Task DisconnectAsync()
    {
        if (_state != VpnState.Connected)
            return;

        _state = VpnState.Disconnecting;
        if (_process != null && !_process.HasExited)
        {
            _process.Kill();
            _process.Dispose();
            _process = null;
        }

        _logger.LogInformation("Removing adapter...");
        await _wintun.DeleteAdapterAsync("VpnClient");

        _state = VpnState.Disconnected;
        _logger.LogInformation("Disconnected");
    }
}
