# VpnClient

A Windows-first AmneziaWG/WireGuard desktop client built with .NET 8 and Avalonia UI.

## Projects
- **Core** – interfaces and models shared across the application.
- **Infrastructure** – implementations of services (e.g., `VpnService`, `WintunService`).
- **UI** – Avalonia desktop application using MVVM.

## Build
1. Install [.NET 8 SDK](https://dotnet.microsoft.com/).
2. Restore packages and build:

```bash
dotnet build VpnClient.sln
dotnet test
```

The UI project `VpnClient.UI` is the startup project.
## Runtime requirements

For the autonomous packaged build, the app ships its own Windows runtime under `runtime/wireguard`:

- `amneziawg.exe`
- `awg.exe`
- `wintun.dll`

Use the publish script in [deploy/client/README.md](/c:/Users/rrese/source/repos/vpn/deploy/client/README.md) to build a clean-machine bundle.

Installer output:

- [YourVpnClient-0.1.0-local.msi](/c:/Users/rrese/source/repos/vpn/artifacts/client-installer/win-x64/YourVpnClient-0.1.0-local.msi)
