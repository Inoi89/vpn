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

Current live state:

- `VpnProductPlatform.Api` is already running on `192.168.1.2`
- nginx on `192.168.1.2` has a dedicated `api.etojesim.com` vhost
- direct public access to that vhost is restricted to:
  - `127.0.0.1`
  - `5.61.37.29`

The current API bootstrap uses `EnsureCreated` on startup for the initial schema and seeds the default plan automatically. That makes the first deploy simple, but it also means we should switch to explicit migrations before this moves beyond MVP.

## SMTP

Registration now supports a best-effort welcome email through the `Smtp` configuration section.

Required deploy variables:

- `PRODUCT_PLATFORM_SMTP_ENABLED`
- `PRODUCT_PLATFORM_SMTP_HOST`
- `PRODUCT_PLATFORM_SMTP_PORT`
- `PRODUCT_PLATFORM_SMTP_SECURE_SOCKET_MODE`
- `PRODUCT_PLATFORM_SMTP_USERNAME`
- `PRODUCT_PLATFORM_SMTP_PASSWORD`
- `PRODUCT_PLATFORM_SMTP_FROM_EMAIL`
- `PRODUCT_PLATFORM_SMTP_FROM_NAME`

If SMTP is disabled or temporarily unavailable, registration still succeeds and the API only logs the send failure.

## Frontend Deployment

The cabinet should be a separate static React build on `5.61.37.29` or another edge host.

Recommended behavior:

- browser talks to the cabinet host by IP or domain
- cabinet host serves the SPA
- cabinet host proxies `/api/` to `https://api.etojesim.com`

That keeps the browser flow same-origin on the web host and avoids touching the API nginx outside a single new vhost.

For the current MVP rollout, there is also a simpler temporary path:

- run the cabinet in Docker on `5.61.37.29`
- expose it directly on port `80`
- proxy `/api/` from that container to `http://93.100.54.80/`
- force `Host: api.etojesim.com` on that upstream request

That avoids blocking the cabinet rollout on HTTPS termination for `api.etojesim.com`.

Current live state:

- `5.61.37.29` does not have `docker compose`
- the cabinet is deployed there via plain `docker build` plus `docker run`
- container name: `product-platform-web`
- public entrypoint: `http://5.61.37.29/`
- the SPA proxies `/api/` server-side to the API origin

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

Another current limitation is external TLS for `api.etojesim.com`:

- the origin API is already running on `192.168.1.2`
- a clean HTTP-only nginx vhost exists on the origin
- but Let's Encrypt `HTTP-01` currently fails because `api.etojesim.com` is proxied through Cloudflare and challenge traffic is not reaching the origin webroot as-is

Until Cloudflare proxying is relaxed or DNS-based validation is introduced, the safe MVP path is:

- keep the API origin on HTTP behind the new host vhost
- let the cabinet host proxy server-side to that HTTP origin
- do not expose the raw API to browsers directly
