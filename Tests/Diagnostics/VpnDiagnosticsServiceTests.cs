using VpnClient.Core.Interfaces;
using VpnClient.Core.Models;
using VpnClient.Core.Models.Diagnostics;
using VpnClient.Infrastructure.Diagnostics;
using Xunit;

namespace VpnClient.Tests.Diagnostics;

public sealed class VpnDiagnosticsServiceTests
{
    [Theory]
    [MemberData(nameof(ImportFailureCases))]
    public async Task CaptureSnapshotAsync_MapsImportValidationFailures(Exception exception, ImportValidationStage expectedStage)
    {
        var service = CreateService();

        service.RecordImportValidationError(exception, "C:\\temp\\sample.vpn");

        var snapshot = await service.CaptureSnapshotAsync();

        Assert.Single(snapshot.ImportValidationErrors);
        var error = snapshot.ImportValidationErrors[0];
        Assert.Equal(expectedStage, error.Stage);
        Assert.Equal("sample.vpn", error.FileName);
        Assert.Equal("C:\\temp\\sample.vpn", error.SourcePath);
    }

    public static TheoryData<Exception, ImportValidationStage> ImportFailureCases => new()
    {
        { new ArgumentException("Path is required."), ImportValidationStage.Path },
        { new FileNotFoundException("Configuration file was not found."), ImportValidationStage.File },
        { new InvalidOperationException("The imported file is not a valid WireGuard/AmneziaWG config."), ImportValidationStage.Format },
        { new InvalidOperationException("The imported .vpn file is malformed."), ImportValidationStage.Decode },
        { new InvalidOperationException("The imported .vpn file does not contain a usable tunnel config."), ImportValidationStage.Parse }
    };

    [Fact]
    public async Task CaptureSnapshotAsync_ReturnsProfileLogsAndTrafficStats()
    {
        var profile = BuildProfile();

        var service = CreateService(
            profile,
            new ConnectionState
            {
                Status = RuntimeConnectionStatus.Connected,
                AdapterName = "AmneziaDaemon",
                ProfileId = profile.Id,
                ProfileName = profile.DisplayName,
                Endpoint = "45.136.49.191:45393",
                Address = "10.8.1.2/32",
                LatestHandshakeAtUtc = DateTimeOffset.FromUnixTimeSeconds(1710000200),
                ReceivedBytes = 400,
                SentBytes = 600,
                AdapterPresent = true,
                TunnelActive = true,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });

        service.RecordConnectionLog("Adapter created", DiagnosticsLogLevel.Information, "vpn", "VpnService");
        service.RecordConnectionLog(new LogEntry(new DateTime(2026, 3, 18, 10, 0, 0), "Connected"), DiagnosticsLogLevel.Information, "vpn", "VpnService");

        var snapshot = await service.CaptureSnapshotAsync();

        Assert.Equal(RuntimeConnectionStatus.Connected, snapshot.ConnectionState.Status);
        Assert.Same(profile, snapshot.CurrentProfile);
        Assert.Equal(2, snapshot.ConnectionLogs.Count);
        Assert.NotNull(snapshot.TrafficStats);
        Assert.Equal(1, snapshot.TrafficStats!.PeerCount);
        Assert.Equal(400, snapshot.TrafficStats.TotalBytesReceived);
        Assert.Equal(600, snapshot.TrafficStats.TotalBytesSent);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1710000200), snapshot.TrafficStats.LastHandshakeUtc);
    }

    private static VpnDiagnosticsService CreateService(ImportedServerProfile? profile = null, ConnectionState? state = null)
    {
        return new VpnDiagnosticsService(
            new FakeProfileRepository(profile),
            new FakeRuntimeAdapter(state ?? ConnectionState.Disconnected("VpnClient")));
    }

    private static ImportedServerProfile BuildProfile()
    {
        var config = new TunnelConfig(
            TunnelConfigFormat.AmneziaAwgNative,
            "[Interface]\nAddress = 10.8.1.2/32",
            [],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Address"] = "10.8.1.2/32",
                ["DNS"] = "8.8.8.8, 8.8.4.4",
                ["PrivateKey"] = "client"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PublicKey"] = "server",
                ["AllowedIPs"] = "0.0.0.0/0, ::/0",
                ["Endpoint"] = "45.136.49.191:45393"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            "10.8.1.2/32",
            ["8.8.8.8", "8.8.4.4"],
            "1280",
            ["0.0.0.0/0", "::/0"],
            25,
            "45.136.49.191:45393",
            "server",
            null);

        return new ImportedServerProfile(
            Guid.NewGuid(),
            "test",
            new ImportedTunnelConfig(
                "test",
                "test.conf",
                @"C:\temp\test.conf",
                TunnelConfigFormat.AmneziaAwgNative,
                DateTimeOffset.Parse("2026-03-18T09:15:00Z"),
                "[Interface]\nAddress = 10.8.1.2/32",
                null,
                config),
            DateTimeOffset.Parse("2026-03-18T09:15:00Z"),
            DateTimeOffset.Parse("2026-03-18T09:15:00Z"));
    }

    private sealed class FakeProfileRepository : IProfileRepository
    {
        private readonly ImportedServerProfile? _profile;

        public FakeProfileRepository(ImportedServerProfile? profile)
        {
            _profile = profile;
        }

        public Task<ProfileCollectionState> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProfileCollectionState(_profile?.Id, _profile is null ? [] : [_profile]));
        }

        public Task<ProfileCollectionState> AddAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ProfileCollectionState> DeleteAsync(Guid profileId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ProfileCollectionState> RenameAsync(Guid profileId, string displayName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ProfileCollectionState> SetActiveAsync(Guid profileId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeRuntimeAdapter : IVpnRuntimeAdapter
    {
        private readonly ConnectionState _state;

        public FakeRuntimeAdapter(ConnectionState state)
        {
            _state = state;
        }

        public ConnectionState CurrentState => _state;
        public event Action<ConnectionState>? StateChanged
        {
            add { }
            remove { }
        }
        public Task<ConnectionState> ConnectAsync(ImportedServerProfile profile, CancellationToken cancellationToken = default) => Task.FromResult(_state);
        public Task<ConnectionState> DisconnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(_state);
        public Task<ConnectionState> GetStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(_state);
    }
}
