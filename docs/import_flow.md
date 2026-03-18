# Import Flow

Snapshot date: `2026-03-18`

This document describes the implemented import behavior for `.vpn` and `.conf`.
The import layer is designed around one hard rule:

No loss of DNS, MTU, AllowedIPs, keepalive, or AWG metadata.

## 1. Supported Inputs

Supported source formats:

- `.vpn`
- `.conf`

The normalized output type is [ImportedTunnelConfig.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/ImportedTunnelConfig.cs), backed by [TunnelConfig.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/TunnelConfig.cs).

## 2. `.vpn` Pipeline

Implementation: [AmneziaImportService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Import/AmneziaImportService.cs)

Pipeline:

1. Read the file as text
2. Strip `vpn://` if present
3. Base64url decode the payload
4. Try Qt/qCompress framing offsets
5. Decompress with zlib
6. Parse JSON
7. Recursively search for embedded `last_config` or `config`
8. Extract the raw embedded tunnel config
9. Normalize it into `TunnelConfig`

Preserved data:

- raw source payload
- decoded raw package JSON
- display name from embedded metadata when available
- endpoint
- keys
- DNS
- MTU
- AllowedIPs
- keepalive
- AWG fields `J*`, `S*`, `H*`, `I*`

The importer is intentionally tolerant of nested JSON strings because Amnezia packages often place the real config under nested serialized JSON.

## 3. `.conf` Pipeline

Implementation: [AmneziaImportService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Import/AmneziaImportService.cs)

Pipeline:

1. Read the file as text
2. Normalize line endings
3. Require `[Interface]` and `[Peer]`
4. Parse line-by-line into:
   - blank
   - comment
   - section header
   - key/value
   - unknown
5. Preserve every parsed line in [ConfigLine.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/ConfigLine.cs)
6. Extract typed fields while retaining full raw config

Preserved typed fields:

- `Address`
- `DNS`
- `MTU`
- `PrivateKey`
- `PublicKey`
- `PresharedKey`
- `AllowedIPs`
- `PersistentKeepalive`
- `Endpoint`
- all AWG families `J*`, `S*`, `H*`, `I*`

If AWG fields are absent, the format is normalized as `WireGuardConf`.
If AWG fields are present, the format becomes `AmneziaAwgNative`.

## 4. Normalization Strategy

Normalization result:

- `ImportedTunnelConfig`
  - file name
  - source path
  - source format
  - raw source
  - optional raw package JSON
  - normalized `TunnelConfig`

Then [ImportProfileUseCase.cs](/c:/Users/rrese/source/repos/vpn/Application/Profiles/ImportProfileUseCase.cs) wraps that import result into a persisted [ImportedServerProfile.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/ImportedServerProfile.cs).

Important design choice:

- The persisted profile stores the full import result, not a lossy projection.

That is what keeps the client runtime path from forgetting AWG or `.vpn` metadata later.

## 5. Validation Behavior

Hard failures:

- empty or missing path
- missing file
- invalid `.conf` without both required sections
- invalid `.vpn` base64 payload
- invalid zlib payload
- missing embedded tunnel config in `.vpn`

Soft issues are preserved as runtime/import warnings instead of being silently dropped.

## 6. Why This Matters

The observed field bug pattern is:

- server provisioning looks correct
- handshake exists
- traffic still diverges from upstream Amnezia behavior

That means import cannot be a thin string loader.
The client must carry as much semantic information forward as possible into runtime activation.

## 7. Tests

Covered by:

- [ImportServiceTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/ImportServiceTests.cs)

Current tests verify:

- `.conf` import retains DNS, MTU, AllowedIPs, keepalive, endpoint, and AWG fields
- `.vpn` import decodes package metadata and extracts embedded tunnel config correctly
