# Product Platform Architecture

Snapshot date: `2026-03-19`

## Purpose

`VpnProductPlatform` is a separate public-facing product layer that should sit above the existing internal control plane.

It is responsible for:

- account registration and login
- subscription and entitlement model
- device enrollment
- anti-sharing limits
- future billing and personal cabinet

It is not responsible for:

- polling VPN nodes
- parsing `wg show`
- maintaining runtime session state on nodes
- operator fleet management

Those remain in the existing `VpnControlPlane`.

## Solution

- [VpnProductPlatform.sln](/c:/Users/rrese/source/repos/vpn/VpnProductPlatform.sln)

## Project Layout

- [VpnProductPlatform.Domain](/c:/Users/rrese/source/repos/vpn/src/ProductPlatform/VpnProductPlatform.Domain/VpnProductPlatform.Domain.csproj)
- [VpnProductPlatform.Application](/c:/Users/rrese/source/repos/vpn/src/ProductPlatform/VpnProductPlatform.Application/VpnProductPlatform.Application.csproj)
- [VpnProductPlatform.Infrastructure](/c:/Users/rrese/source/repos/vpn/src/ProductPlatform/VpnProductPlatform.Infrastructure/VpnProductPlatform.Infrastructure.csproj)
- [VpnProductPlatform.Contracts](/c:/Users/rrese/source/repos/vpn/src/ProductPlatform/VpnProductPlatform.Contracts/VpnProductPlatform.Contracts.csproj)
- [VpnProductPlatform.Api](/c:/Users/rrese/source/repos/vpn/src/ProductPlatform/VpnProductPlatform.Api/VpnProductPlatform.Api.csproj)
- [VpnProductPlatform.Tests](/c:/Users/rrese/source/repos/vpn/tests/ProductPlatform/VpnProductPlatform.Tests/VpnProductPlatform.Tests.csproj)
- [frontend/product-platform-web](/c:/Users/rrese/source/repos/vpn/frontend/product-platform-web)

## Current Implemented Slice

The current skeleton already includes:

- `Account`
- `Device`
- `SubscriptionPlan`
- `Subscription`
- `AccessGrant`
- `AccountSession`
- JWT login
- account registration
- best-effort welcome email on registration via SMTP
- refresh tokens with rotation
- session list and revoke
- authenticated logout
- access token validation against live account session state
- default trial subscription on registration
- device registration with plan-based device limit
- device revoke
- PostgreSQL persistence through EF Core

## Current API Surface

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`
- `GET /api/me`
- `GET /api/devices`
- `POST /api/devices`
- `DELETE /api/devices/{deviceId}`
- `GET /api/sessions`
- `DELETE /api/sessions/{sessionId}`
- `GET /api/access-grants`
- `GET /healthz`

`AccessGrant` already exists in the domain model. The cabinet can read grant history, but issuance endpoints are intentionally not exposed yet.

## Current Product Assumption

For bootstrap purposes, registration automatically grants a trial subscription based on the default active plan seeded into the database.

This is intentionally temporary.

It exists so the solution is runnable before a billing provider is integrated.

## Independent Deployment

This solution is designed to be deployed independently from the control plane.

Sample deployment:

- [docker-compose.sample.yml](/c:/Users/rrese/source/repos/vpn/deploy/product-platform/docker-compose.sample.yml)
- [docker-compose.lan.yml](/c:/Users/rrese/source/repos/vpn/deploy/product-platform/docker-compose.lan.yml)
- [Dockerfile](/c:/Users/rrese/source/repos/vpn/src/ProductPlatform/VpnProductPlatform.Api/Dockerfile)

Default local port mapping:

- API: `http://localhost:7101`
- PostgreSQL: `localhost:5434`

## Run Locally

```powershell
dotnet build VpnProductPlatform.sln
dotnet test VpnProductPlatform.sln
dotnet run --project src/ProductPlatform/VpnProductPlatform.Api/VpnProductPlatform.Api.csproj
```

## Immediate Next Work

The next implementation steps should be:

1. add enrollment endpoint that requests access issuance from the control plane
2. add billing provider abstraction and webhook handling
3. harden refresh-token persistence and cleanup jobs
4. add audit trail and operator tooling around device revoke and entitlement changes
