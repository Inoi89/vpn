# SimVPN

Minimal GUI VPN client built with Electron and WireGuard.

### Usage

1. Place `wireguard-go.exe` and `wintun.dll` next to the application files.
2. Run `npm start` during development or build with `npm run dist`.
3. Use the **📄 Импорт** button to select a `.conf` file. The file is copied to `temp/imported.conf`.
4. Press **🟢 GO VPN** to launch `wireguard-go.exe` with the cleaned
   configuration. The adapter is created via `wintun.dll` directly.
5. Press **🔴 STOP VPN** to delete the interface.
