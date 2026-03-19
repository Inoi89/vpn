# Product Platform Deploy

This folder contains the deployable product-layer stack that sits above `VpnControlPlane`.

## Intended Layout

- API host: `192.168.1.2`
- public API hostname: `api.etojesim.com`
- web host for the personal cabinet: `5.61.37.29`

## Current API Container

The API stack is meant to run as a separate compose project using:

- [docker-compose.lan.yml](./docker-compose.lan.yml)
- [api.etojesim.com.conf.sample](./nginx/api.etojesim.com.conf.sample)

The API container publishes only on `127.0.0.1:7201` on the API host. Nginx terminates TLS and proxies to that local port.

On first startup the API bootstrap auto-creates the schema with `EnsureCreated` and seeds the default trial plan. That is fine for the MVP stack, but it should move to explicit migrations later.

## Web Cabinet

The future React cabinet should be deployed as static files on the web host and reverse-proxy `/api/` to `https://api.etojesim.com/`.

If we want to tighten the API surface further, the API host can also be firewalled so inbound `443` is allowed only from the cabinet host public IP.

Sample nginx config:

- [product-platform-web.local.conf.sample](./nginx/product-platform-web.local.conf.sample)

## Environment

Copy [`.env.example`](./.env.example) to `.env` and set:

- `PRODUCT_PLATFORM_POSTGRES_PASSWORD`
- `PRODUCT_PLATFORM_JWT_SIGNING_KEY`

## Notes

- Do not edit the existing nginx server blocks in place.
- Add a new `sites-available` file and symlink it into `sites-enabled`.
- Keep the product platform stack isolated from the existing control-plane stack.
