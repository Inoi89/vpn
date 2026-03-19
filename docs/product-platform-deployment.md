# Product Platform Deployment

Snapshot date: `2026-03-19`

## Host Split

The recommended layout is:

- `192.168.1.2`: `VpnControlPlane` plus the new `VpnProductPlatform.Api`
- `5.61.37.29`: future public cabinet/static web host

## Current nginx Findings on `192.168.1.2`

The live nginx tree is cleanly split by site files:

- `itsagame.etojesim.com` is defined in its own file under `sites-available`
- it is enabled through a symlink in `sites-enabled`
- `conf.d` is empty

That means the safe way to add `api.etojesim.com` is:

1. create a new file in `sites-available`
2. symlink it into `sites-enabled`
3. reload nginx

Do not edit the existing site file unless the change is meant for that host.

## API Deployment

The API should run as a separate compose project using:

- [deploy/product-platform/docker-compose.lan.yml](/c:/Users/rrese/source/repos/vpn/deploy/product-platform/docker-compose.lan.yml)

Recommended binding:

- API: `127.0.0.1:7201 -> 8080`
- PostgreSQL: internal container-only network

The nginx vhost for `api.etojesim.com` should proxy to `http://127.0.0.1:7201`.

The current API bootstrap uses `EnsureCreated` on startup for the initial schema and seeds the default plan automatically. That makes the first deploy simple, but it also means we should switch to explicit migrations before this moves beyond MVP.

## Frontend Deployment

The cabinet should be a separate static React build on `5.61.37.29` or another edge host.

Recommended behavior:

- browser talks to the cabinet host by IP or domain
- cabinet host serves the SPA
- cabinet host proxies `/api/` to `https://api.etojesim.com`

That keeps the browser flow same-origin on the web host and avoids touching the API nginx outside a single new vhost.

If we want to tighten the public surface further, the API host can additionally be firewalled so that inbound 443 is allowed only from the cabinet host public IP. The browser would still talk only to the cabinet host; the cabinet host would be the sole server-side caller of `api.etojesim.com`.

## API Origin Policy

The Product Platform API now exposes CORS configuration via `Cors:AllowedOrigins`.

That is needed for local development and any future direct browser integration.

For the first deploy, the preferred production path is still:

- cabinet host proxies to API
- API stays behind nginx on its own host
- no existing control-plane nginx rules are modified

## Minimum Release Flow

1. build and test the `VpnProductPlatform.sln`
2. deploy the API compose on `192.168.1.2`
3. add the new `api.etojesim.com` nginx vhost
4. deploy the cabinet static site on `5.61.37.29`
5. point the cabinet to `https://api.etojesim.com`

## Current Limitation

I was able to inspect `5.61.37.29` over SSH only far enough to confirm it is currently running the VPN stack and does not already have nginx bound on 80/443.

That makes it a clean candidate for the future cabinet host.
