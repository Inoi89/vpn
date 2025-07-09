# SimVPN

Minimal GUI VPN client built with Electron and WireGuard.

### Usage

1. Place `wg.exe` and `wintun.dll` next to the application files.
2. Run `npm start` during development or build with `npm run dist`.
3. Use the **📄 Импорт** button to select a `.conf` file. The file is copied to `temp/imported.conf`.
4. Press **🟢 GO VPN** to enable the `SimVPN` interface. The imported config is
   cleaned so that only the `PrivateKey` and `[Peer]` sections are passed to
   `wg.exe`. The interface is created if necessary, and Address/DNS values are
   applied via `netsh`.
5. Press **🔴 STOP VPN** to delete the interface.
