using System.Text.Json;
using VpnClient.Application.Profiles;
using VpnClient.Core.Models;
using VpnClient.Infrastructure.Persistence;
using Xunit;

public sealed class ProfileRepositoryTests
{
    [Fact]
    public async Task AddRenameSetActiveAndDelete_PersistsCollectionState()
    {
        var tempDirectory = CreateTempDirectory();
        var statePath = Path.Combine(tempDirectory, "profiles.json");
        var repository = new JsonProfileRepository(statePath);

        var profile1 = new ImportedServerProfile(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Primary",
            CreateImportedTunnelConfig(
                "Primary",
                "primary.vpn",
                @"C:\profiles\primary.vpn",
                TunnelConfigFormat.AmneziaVpn,
                "45.136.49.191:45393",
                "10.8.1.2/32",
                ["1.1.1.1", "1.0.0.1"]),
            DateTimeOffset.Parse("2026-03-18T10:00:00Z"),
            DateTimeOffset.Parse("2026-03-18T10:00:00Z"));

        var profile2 = new ImportedServerProfile(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Backup",
            CreateImportedTunnelConfig(
                "Backup",
                "backup.conf",
                @"C:\profiles\backup.conf",
                TunnelConfigFormat.AmneziaAwgNative,
                "37.1.197.163:45393",
                "10.8.1.3/32",
                ["8.8.8.8", "8.8.4.4"]),
            DateTimeOffset.Parse("2026-03-18T11:00:00Z"),
            DateTimeOffset.Parse("2026-03-18T11:00:00Z"));

        var afterAdd = await repository.AddAsync(profile1);
        Assert.Equal(profile1.Id, afterAdd.ActiveProfileId);
        Assert.Single(afterAdd.Profiles);

        var afterRename = await repository.RenameAsync(profile1.Id, "Primary Node");
        Assert.Equal("Primary Node", afterRename.Profiles.Single().DisplayName);

        var afterSecondAdd = await repository.AddAsync(profile2);
        Assert.Equal(2, afterSecondAdd.Profiles.Count);

        var afterSetActive = await repository.SetActiveAsync(profile2.Id);
        Assert.Equal(profile2.Id, afterSetActive.ActiveProfileId);

        var afterDelete = await repository.DeleteAsync(profile2.Id);
        Assert.Null(afterDelete.ActiveProfileId);
        Assert.Single(afterDelete.Profiles);
        Assert.Equal(profile1.Id, afterDelete.Profiles.Single().Id);

        var persisted = JsonSerializer.Deserialize<ProfileCollectionState>(await File.ReadAllTextAsync(statePath));
        Assert.NotNull(persisted);
        Assert.Equal(profile1.Id, persisted!.Profiles.Single().Id);
    }

    [Fact]
    public void DefaultStoragePath_IsAppDataBased()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YourVpnClient",
            "profiles.json");

        Assert.Equal(expected, JsonProfileRepository.GetDefaultStoragePath());
    }

    [Fact]
    public async Task ListProfilesUseCase_SortsActiveProfileFirst()
    {
        var tempDirectory = CreateTempDirectory();
        var statePath = Path.Combine(tempDirectory, "profiles.json");
        var repository = new JsonProfileRepository(statePath);
        var useCase = new ListProfilesUseCase(repository);

        var first = new ImportedServerProfile(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "First",
            CreateImportedTunnelConfig(
                "First",
                "first.vpn",
                @"C:\profiles\first.vpn",
                TunnelConfigFormat.AmneziaVpn,
                null,
                null,
                []),
            DateTimeOffset.Parse("2026-03-18T09:00:00Z"),
            DateTimeOffset.Parse("2026-03-18T09:00:00Z"));

        var second = new ImportedServerProfile(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "Second",
            CreateImportedTunnelConfig(
                "Second",
                "second.vpn",
                @"C:\profiles\second.vpn",
                TunnelConfigFormat.AmneziaVpn,
                null,
                null,
                []),
            DateTimeOffset.Parse("2026-03-18T12:00:00Z"),
            DateTimeOffset.Parse("2026-03-18T12:00:00Z"));

        await repository.AddAsync(first);
        await repository.AddAsync(second);
        await repository.SetActiveAsync(second.Id);

        var snapshot = await useCase.ExecuteAsync();
        Assert.Equal(second.Id, snapshot.ActiveProfileId);
        Assert.Equal(second.Id, snapshot.Profiles.First().Id);
    }

    [Fact]
    public async Task AddAsync_ReplacesExistingManagedProfile_ForSameAccountDevice()
    {
        var tempDirectory = CreateTempDirectory();
        var statePath = Path.Combine(tempDirectory, "profiles.json");
        var repository = new JsonProfileRepository(statePath);

        var accountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var deviceId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var first = new ImportedServerProfile(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            "Managed A",
            CreateImportedTunnelConfig(
                "Managed A",
                "managed-a.vpn",
                @"managed://grant-a",
                TunnelConfigFormat.AmneziaVpn,
                "5.61.37.29:443",
                "10.8.1.12/32",
                ["8.8.8.8", "8.8.4.4"]),
            DateTimeOffset.Parse("2026-03-19T09:00:00Z"),
            DateTimeOffset.Parse("2026-03-19T09:00:00Z"),
            new ManagedProfileBinding(
                accountId,
                "alex@example.com",
                deviceId,
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa"),
                Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
                "amnezia-vpn"));

        var second = new ImportedServerProfile(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            "Managed B",
            CreateImportedTunnelConfig(
                "Managed B",
                "managed-b.vpn",
                @"managed://grant-b",
                TunnelConfigFormat.AmneziaVpn,
                "5.61.40.132:443",
                "10.8.1.13/32",
                ["1.1.1.1", "1.0.0.1"]),
            DateTimeOffset.Parse("2026-03-19T10:00:00Z"),
            DateTimeOffset.Parse("2026-03-19T10:00:00Z"),
            new ManagedProfileBinding(
                accountId,
                "alex@example.com",
                deviceId,
                Guid.Parse("99999999-8888-7777-6666-555555555555"),
                Guid.Parse("44444444-3333-2222-1111-000000000000"),
                Guid.Parse("12121212-3434-5656-7878-909090909090"),
                "amnezia-vpn"));

        var afterFirstAdd = await repository.AddAsync(first);
        var preservedLocalId = afterFirstAdd.Profiles.Single().Id;

        var afterSecondAdd = await repository.AddAsync(second);

        var stored = Assert.Single(afterSecondAdd.Profiles);
        Assert.Equal(preservedLocalId, stored.Id);
        Assert.Equal("Managed B", stored.DisplayName);
        Assert.Equal("10.8.1.13/32", stored.Address);
        Assert.NotNull(stored.ManagedProfile);
        Assert.Equal(second.ManagedProfile!.AccessGrantId, stored.ManagedProfile!.AccessGrantId);
        Assert.Equal(preservedLocalId, afterSecondAdd.ActiveProfileId);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vpn-client-profiles-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static ImportedTunnelConfig CreateImportedTunnelConfig(
        string displayName,
        string fileName,
        string sourcePath,
        TunnelConfigFormat format,
        string? endpoint,
        string? address,
        IReadOnlyList<string> dnsServers)
    {
        return new ImportedTunnelConfig(
            displayName,
            fileName,
            sourcePath,
            format,
            DateTimeOffset.Parse("2026-03-18T08:00:00Z"),
            BuildRawConfig(address, dnsServers, endpoint),
            format == TunnelConfigFormat.AmneziaVpn ? "{\"containers\":[]}" : null,
            new TunnelConfig(
                format,
                BuildRawConfig(address, dnsServers, endpoint),
                [],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                address,
                dnsServers.ToArray(),
                "1280",
                endpoint is null ? Array.Empty<string>() : new[] { "0.0.0.0/0", "::/0" },
                25,
                endpoint,
                endpoint is null ? null : "server-key",
                endpoint is null ? null : "psk-key"));
    }

    private static string BuildRawConfig(string? address, IReadOnlyList<string> dnsServers, string? endpoint)
    {
        var interfaceLines = new List<string>
        {
            "[Interface]"
        };

        if (!string.IsNullOrWhiteSpace(address))
        {
            interfaceLines.Add($"Address = {address}");
        }

        if (dnsServers.Count > 0)
        {
            interfaceLines.Add($"DNS = {string.Join(", ", dnsServers)}");
        }

        interfaceLines.Add("PrivateKey = client-key");
        interfaceLines.Add(string.Empty);
        interfaceLines.Add("[Peer]");

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            interfaceLines.Add("PublicKey = server-key");
            interfaceLines.Add("PresharedKey = psk-key");
            interfaceLines.Add("AllowedIPs = 0.0.0.0/0, ::/0");
            interfaceLines.Add($"Endpoint = {endpoint}");
            interfaceLines.Add("PersistentKeepalive = 25");
        }

        return string.Join(Environment.NewLine, interfaceLines);
    }
}
