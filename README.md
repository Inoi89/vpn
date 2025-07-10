# VpnClient

A simple WireGuard-based VPN client built with .NET 8 and Avalonia UI.

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

- `wintun.dll` must be accessible by the application (place it next to the built executable or add it to `PATH`).
- `wg.exe` is required to configure the WireGuard adapter and must be available on `PATH` or next to the executable.
- Place your VPN configuration in `vpn.conf` next to the executable.
