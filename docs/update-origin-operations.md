# Update Origin Operations

Snapshot date: `2026-03-19`

This document is the concrete operations runbook for the desktop client update origin.

## 1. Current Origin

- host: `37.1.197.163`
- domain: `vpn.udni.ru`
- role: static HTTPS origin for desktop client releases
- web root: `/srv/vpn-updates`
- active channel path: `/srv/vpn-updates/vpn-client/stable`

## 2. What Is Hosted

Current stable layout:

- `https://vpn.udni.ru/vpn-client/stable/update-manifest.json`
- `https://vpn.udni.ru/vpn-client/stable/YourVpnClient-0.1.6.msi`
- `https://vpn.udni.ru/vpn-client/stable/VpnClient-win-x64.zip`

The client checks only the manifest URL. The manifest then points to the MSI.

## 3. Server-Side Stack

Installed on `37.1.197.163`:

- `nginx`
- `certbot`
- `python3-certbot-nginx`

TLS:

- Let's Encrypt certificate for `vpn.udni.ru`
- current cert path on server:
  - `/etc/letsencrypt/live/vpn.udni.ru/fullchain.pem`
  - `/etc/letsencrypt/live/vpn.udni.ru/privkey.pem`

## 4. Nginx Layout

Site file:

- `/etc/nginx/sites-available/vpn.udni.ru`

Symlink:

- `/etc/nginx/sites-enabled/vpn.udni.ru`

Behavior:

- `/` redirects to `/vpn-client/stable/`
- `/vpn-client/stable/update-manifest.json` is served with `Cache-Control: no-store`
- MSI and ZIP payloads are served as static files

## 5. Release Procedure

From the repo root:

1. Build portable payload:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\client\publish-win-x64.ps1 -Configuration Release -RuntimeIdentifier win-x64 -Version 0.1.6 -ZipPackage
```

2. Build MSI:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\client\build-msi.ps1 -Configuration Release -RuntimeIdentifier win-x64 -Version 0.1.6
```

3. Publish to origin:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\client\publish-update-origin.ps1 -Version 0.1.6 -ServerPassword <root-password> -ServerHost 37.1.197.163 -Domain vpn.udni.ru -PackageBaseUrl https://vpn.udni.ru/vpn-client/stable -ReleaseNotes "0.1.6 prefers the local Amnezia daemon when present to avoid mixed runtime paths." -UploadZip
```

What that does:

- regenerates `update-manifest.json`
- uploads MSI
- uploads manifest
- optionally uploads ZIP
- leaves the server serving the new release immediately

## 6. Validation Checklist

After publishing:

1. Check manifest:
   - `curl -I https://vpn.udni.ru/vpn-client/stable/update-manifest.json`
2. Check MSI:
   - `curl -I https://vpn.udni.ru/vpn-client/stable/YourVpnClient-<version>.msi`
3. Check server-side files:
   - `ls -lh /srv/vpn-updates/vpn-client/stable`
4. Check local client:
   - an installed `0.1.0-local` client should report `UpdateAvailable`
   - installation should proceed through `VpnClient.Updater.exe`

## 7. Failure Modes

If the client does not detect an update:

- manifest URL is wrong
- manifest version is not newer
- the installed client still points to another feed

If the client downloads but refuses to install:

- manifest hash does not match MSI
- MSI was replaced after manifest generation
- signer thumbprint is required and does not match

If HTTPS fails:

- DNS for `vpn.udni.ru` is wrong
- Let's Encrypt certificate expired or was not renewed
- `nginx` site config was changed incorrectly

## 8. Next Production Hardening

- sign the MSI with a real code-signing certificate
- put signer thumbprint into the manifest
- add `beta` and `internal` channels under separate paths
- consider moving payloads behind object storage/CDN later without changing the client contract
