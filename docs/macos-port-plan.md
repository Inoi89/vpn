# macOS Port Plan

## Goal

Сделать `etoVPN` под macOS с тем же UX и почти тем же пользовательским флоу, что и у текущего Windows-клиента:

- onboarding
- login / account
- managed enrollment
- import `.vpn` / `.conf`
- connect / disconnect
- tray / menu bar control
- update flow

Ключевое ограничение: это **не** "пересобрать Windows-клиент под Mac".  
UI можно переиспользовать в большой степени, но VPN runtime, kill switch, installer и updater на macOS должны быть отдельными.

## Current Status

Phase 1 is now implemented in the source tree:

- Windows-specific single-instance bootstrap is isolated behind a desktop bootstrap layer.
- macOS-safe startup now falls back to no-op runtime, kill switch, diagnostics, and updater services.
- Tray and activation wiring is only enabled on Windows.

Phase 2 is now started in the source tree:

- The desktop macOS adapter now speaks a stable bridge envelope instead of ad-hoc JSON commands.
- The macOS bridge contract is documented in `docs/macos-runtime-bridge-contract.md`.
- A native `native/macos/` scaffold now exists for the bridge helper, packet tunnel, and shared models.
- The native bridge now has a newline-delimited request/response processing skeleton.
- The bridge and packet tunnel now share a staged profile handoff path through a common control-store filename.
- The next real runtime step is to replace that temporary staged-file handoff with `NETunnelProviderProtocol.providerConfiguration`, matching the upstream Apple path.
- The packet tunnel now has a scaffold builder for `NEPacketTunnelNetworkSettings` and a provider-side status message path.

## Что уже можно переиспользовать

Эти части уже в основном кроссплатформенные:

- `Avalonia` UI:
  - [Program.cs](/c:/Users/rrese/source/repos/vpn/UI/Program.cs)
  - [App.axaml](/c:/Users/rrese/source/repos/vpn/UI/App.axaml)
  - [MainWindow.axaml](/c:/Users/rrese/source/repos/vpn/UI/Views/MainWindow.axaml)
- ViewModel и UI state:
  - [MainWindowViewModel.cs](/c:/Users/rrese/source/repos/vpn/UI/ViewModels/MainWindowViewModel.cs)
- import `.vpn` / `.conf`:
  - [AmneziaImportService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Import/AmneziaImportService.cs)
  - [AmneziaVpnConfigMaterializer.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Import/AmneziaVpnConfigMaterializer.cs)
- локальные профили и settings:
  - [JsonProfileRepository.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Persistence/JsonProfileRepository.cs)
  - [JsonClientSettingsService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Persistence/JsonClientSettingsService.cs)
- auth / enrollment:
  - [JsonProductPlatformAuthService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Auth/JsonProductPlatformAuthService.cs)
  - [ProductPlatformEnrollmentService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Auth/ProductPlatformEnrollmentService.cs)
- update manifest logic как концепт:
  - [JsonManifestAppUpdateService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Updates/JsonManifestAppUpdateService.cs)

## Что сейчас намертво завязано на Windows

Это надо либо переписать, либо абстрагировать:

- Windows-only composition root:
  - [Program.cs](/c:/Users/rrese/source/repos/vpn/UI/Program.cs)
  - [App.axaml.cs](/c:/Users/rrese/source/repos/vpn/UI/App.axaml.cs)
- Windows runtime:
  - [BundledAmneziaRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/BundledAmneziaRuntimeAdapter.cs)
  - [AmneziaDaemonRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/AmneziaDaemonRuntimeAdapter.cs)
  - [WindowsFirstVpnRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/WindowsFirstVpnRuntimeAdapter.cs)
- Windows asset bundle:
  - [IWindowsRuntimeAssetLocator.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/IWindowsRuntimeAssetLocator.cs)
- Wintun / `netsh` / `sc.exe`:
  - [WintunService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Services/WintunService.cs)
  - [WindowsKillSwitchService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/WindowsKillSwitchService.cs)
- Windows diagnostics:
  - [WindowsWireGuardDumpReader.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Diagnostics/WindowsWireGuardDumpReader.cs)
  - [WindowsWireGuardDumpParser.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Diagnostics/WindowsWireGuardDumpParser.cs)
- Windows single-instance / app shell:
  - [SingleInstanceCoordinator.cs](/c:/Users/rrese/source/repos/vpn/UI/SingleInstanceCoordinator.cs)
- Windows installer / updater:
  - [Updater/Program.cs](/c:/Users/rrese/source/repos/vpn/Updater/Program.cs)
  - [build-msi.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/build-msi.ps1)
  - [Product.wxs](/c:/Users/rrese/source/repos/vpn/deploy/client/wix/Product.wxs)

## Что уже есть у upstream Amnezia для macOS

Локально в `.research` уже лежит нормальный референс, и это сильно снижает риск.

### Runtime / daemon / firewall / routes

- [macosdaemon.cpp](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/client/platforms/macos/daemon/macosdaemon.cpp)
- [macosfirewall.cpp](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/client/platforms/macos/daemon/macosfirewall.cpp)
- [dnsutilsmacos.cpp](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/client/platforms/macos/daemon/dnsutilsmacos.cpp)
- [wireguardutilsmacos.cpp](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/client/platforms/macos/daemon/wireguardutilsmacos.cpp)

### Packet tunnel / Network Extension

- [PacketTunnelProvider.swift](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/client/platforms/ios/PacketTunnelProvider.swift)
- [PacketTunnelProvider+WireGuard.swift](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/client/platforms/ios/PacketTunnelProvider+WireGuard.swift)
- [macos_ne.cmake](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/client/cmake/macos_ne.cmake)

Примечание: у них macOS и iOS местами делят один `NetworkExtension` path, поэтому часть нужного кода лежит под `platforms/ios`, но реально используется и для macOS-сборки.

### Tray / menu bar

- [macosstatusicon.mm](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/client/platforms/macos/macosstatusicon.mm)

### Packaging / signing

- [build_macos.sh](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/deploy/build_macos.sh)
- [build_macos_ne.sh](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/deploy/build_macos_ne.sh)

### Kill switch / pf

- [amn.conf](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/deploy/data/macos/pf/amn.conf)
- [amn.100.blockAll.conf](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/deploy/data/macos/pf/amn.100.blockAll.conf)
- [amn.200.allowVPN.conf](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/deploy/data/macos/pf/amn.200.allowVPN.conf)

## Целевая архитектура

### Общий принцип

Оставляем текущий `etoVPN` как один продукт, но делим platform layer:

- `Core`
- `Application`
- общая часть `Infrastructure`
- `UI` на Avalonia
- `Infrastructure.Runtime.Windows`
- `Infrastructure.Runtime.Macos`
- `Updater.Windows`
- `Updater.Macos`

### macOS runtime path

Для macOS нужен отдельный нативный runtime, а не Windows fallback.

Предлагаемая схема:

1. `Avalonia` UI остаётся основным приложением.
2. UI общается с локальным macOS runtime bridge через IPC.
3. Bridge управляет:
   - `NetworkExtension` tunnel
   - DNS / routes
   - kill switch
   - status / handshake / traffic snapshot
4. Bridge использует семантику, близкую к upstream Amnezia.

### IPC

У нас уже есть абстракция:

- [IAmneziaDaemonTransport.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/IAmneziaDaemonTransport.cs)

Сейчас она реализована через Windows named pipe:

- [NamedPipeAmneziaDaemonTransport.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/NamedPipeAmneziaDaemonTransport.cs)

Для macOS правильный следующий шаг:

- добавить `UnixDomainSocketAmneziaDaemonTransport`
- держать один и тот же JSON command surface

Это позволит:

- не переписывать весь UI
- переиспользовать текущую модель connect / disconnect / status

## Предлагаемая структура проектов

Минимальный реалистичный набор:

- `Infrastructure/Runtime/Macos/...`
  - `MacosVpnRuntimeAdapter.cs`
  - `MacosKillSwitchService.cs`
  - `UnixDomainSocketAmneziaDaemonTransport.cs`
  - `MacosRuntimeAssetLocator.cs`
  - `MacosWireGuardDumpReader.cs`
- `native/macos/etoVPN.MacBridge`
  - Swift / Objective-C++ helper
  - IPC listener
  - orchestration поверх `NetworkExtension`
- `native/macos/etoVPN.PacketTunnel`
  - Packet Tunnel extension
- `deploy/client-macos/...`
  - `.app`
  - `.pkg`
  - notarization / release scripts

## Порядок работ

### Phase 1. Platform split

Цель: не делать macOS поверх Windows-кода.

Нужно:

- снять `[SupportedOSPlatform(\"windows\")]` с общих entrypoints, где это возможно
- вынести Windows-specific registrations из [Program.cs](/c:/Users/rrese/source/repos/vpn/UI/Program.cs)
- ввести platform bootstrap:
  - `WindowsDesktopPlatformModule`
  - `MacosDesktopPlatformModule`
- отвязать tray, single-instance и updater от жёсткой Windows-реализации

Результат:

- Windows по-прежнему работает
- macOS билд хотя бы стартует как shell без connect

### Phase 2. macOS runtime MVP

Цель: первый рабочий `connect / disconnect / status`.

Нужно:

- реализовать `UnixDomainSocketAmneziaDaemonTransport`
- поднять нативный macOS helper с JSON IPC
- подключить `NetworkExtension`
- материализовать runtime config из наших импортированных профилей
- вернуть в UI:
  - `Connected`
  - `Disconnected`
  - handshake
  - traffic counters

Результат:

- реальный VPN connect на macOS
- импорт ручных конфигов и managed enrollment работают

### Phase 3. macOS shell parity

Цель: поведение 1:1 для пользователя.

Нужно:

- menu bar / tray path
- single-instance
- close-to-tray semantics
- startup restore
- disconnect on full exit
- notifications

Результат:

- UX совпадает с Windows-клиентом

### Phase 4. Packaging and release

Цель: готовый продуктовый артефакт.

Нужно:

- `.app`
- `.pkg`
- code signing
- notarization
- auto-update path для macOS

Результат:

- installable macOS client

## Acceptance criteria

Считать macOS MVP успешным, если пользователь может:

1. Установить клиент.
2. Войти в аккаунт или импортировать legacy config.
3. Нажать `Connect`.
4. Получить реальный трафик, а не только handshake.
5. Закрыть окно в tray / menu bar.
6. После reboot не получать зависший туннель и broken routes.

## Самые рискованные зоны

### 1. Не UI, а runtime

Главный риск не в `Avalonia`, а в:

- `NetworkExtension`
- DNS / routes
- kill switch
- lifecycle tunnel extension

### 2. Apple signing / entitlements

Без этого продукт не выйдет в нормальный install flow.

### 3. Kill switch semantics

На Windows у нас это `netsh advfirewall`.  
На macOS нужно идти по `pf`/upstream path, а не городить свой случайный сетевой хак.

## Практический next step

Если начинать работу прямо сейчас, первый конкретный спринт должен быть таким:

1. Вынести platform bootstrap из [Program.cs](/c:/Users/rrese/source/repos/vpn/UI/Program.cs).
2. Добавить `UnixDomainSocketAmneziaDaemonTransport`.
3. Завести папку `native/macos/`.
4. Подготовить macOS helper skeleton с IPC surface, совпадающим с Windows daemon path.
5. Привязать к нему пустой `MacosVpnRuntimeAdapter`, который пока умеет хотя бы `Unsupported -> Connecting -> Failed` без краша UI.

Только после этого стоит идти в настоящий `NetworkExtension`.

## Decision

Решение: **делать macOS-клиент на том же Avalonia UI, но с отдельным нативным macOS runtime и отдельным packaging path, опираясь на upstream Amnezia как на технический референс.**
