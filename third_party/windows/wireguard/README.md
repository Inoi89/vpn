# Bundled AmneziaWG Runtime Assets

This directory stages the official Windows runtime files that are shipped with the desktop VPN client.

Required now:

- `amneziawg.exe`
- `awg.exe`
- `wintun.dll`

These files are copied into:

`runtime/wireguard`

inside the self-contained publish output.

Why this directory exists:

- the client must be able to run on a clean Windows machine
- relying on PATH-installed VPN binaries is not a product story
- app-local runtime assets make packaging and installer work deterministic

Current source of truth:

- official AmneziaWG Windows MSI `amneziawg-amd64-2.0.0.msi`
- extracted from the signed release artifact into this folder
