# Product Platform Deploy

This folder contains the deployable product-layer stack that sits above `VpnControlPlane`.

## Intended Layout

- API host: `192.168.1.2`
- public API hostname: `api.etojesim.com`
- public cabinet hostname: `etovpn.com`
- web host for the personal cabinet: `5.61.37.29`

## Current API Container

The API stack is meant to run as a separate compose project using:

- [docker-compose.lan.yml](./docker-compose.lan.yml)
- [api.etojesim.com.conf.sample](./nginx/api.etojesim.com.conf.sample)

The API container publishes only on `127.0.0.1:7201` on the API host. Nginx terminates TLS and proxies to that local port.

On first startup the API bootstrap auto-creates the schema with `EnsureCreated` and seeds the default trial plan. That is fine for the MVP stack, but it should move to explicit migrations later.

## Web Cabinet

The future React cabinet should be deployed as static files on the web host and reverse-proxy `/api/` to `https://api.etojesim.com/`.

For the current MVP, there is also a temporary Docker-based web deployment:

- [docker-compose.web.yml](./docker-compose.web.yml)
- [nginx.proxy.conf](/c:/Users/rrese/source/repos/vpn/frontend/product-platform-web/nginx.proxy.conf)

This setup serves the cabinet on `https://etovpn.com` and proxies `/api/` to `http://93.100.54.80/` with `Host: api.etojesim.com`.

Current live rollout uses plain Docker on `5.61.37.29`, not `docker compose`, because that host currently has Docker installed without the compose plugin.

If we want to tighten the API surface further, the API host can also be firewalled so inbound `443` is allowed only from the cabinet host public IP.

Sample nginx config:

- [product-platform-web.local.conf.sample](./nginx/product-platform-web.local.conf.sample)

## Environment

Copy [`.env.example`](./.env.example) to `.env` and set:

- `PRODUCT_PLATFORM_POSTGRES_PASSWORD`
- `PRODUCT_PLATFORM_JWT_SIGNING_KEY`
- `PRODUCT_PLATFORM_SMTP_ENABLED`
- `PRODUCT_PLATFORM_SMTP_HOST`
- `PRODUCT_PLATFORM_SMTP_PORT`
- `PRODUCT_PLATFORM_SMTP_SECURE_SOCKET_MODE`
- `PRODUCT_PLATFORM_SMTP_USERNAME`
- `PRODUCT_PLATFORM_SMTP_PASSWORD`
- `PRODUCT_PLATFORM_SMTP_FROM_EMAIL`
- `PRODUCT_PLATFORM_SMTP_FROM_NAME`
- `PRODUCT_PLATFORM_EMAIL_VERIFICATION_ISSUER`
- `PRODUCT_PLATFORM_EMAIL_VERIFICATION_AUDIENCE`
- `PRODUCT_PLATFORM_EMAIL_VERIFICATION_SIGNING_KEY`
- `PRODUCT_PLATFORM_EMAIL_VERIFICATION_LIFETIME_HOURS`
- `PRODUCT_PLATFORM_EMAIL_VERIFICATION_CABINET_BASE_URL`

## Notes

- Do not edit the existing nginx server blocks in place.
- Add a new `sites-available` file and symlink it into `sites-enabled`.
- Keep the product platform stack isolated from the existing control-plane stack.
- The live `api.etojesim.com` origin on `192.168.1.2` is already restricted to `5.61.37.29` and `127.0.0.1`.
- Registration verification mail is best-effort: account creation should not fail if SMTP is temporarily unavailable.
- The live cabinet now terminates HTTPS on `etovpn.com` with Let's Encrypt and serves the product favicon from `frontend/product-platform-web/public/favicon.ico`.
