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
- email verification by signed link
- best-effort SMTP delivery for verification mail
- refresh tokens with rotation
- session list and revoke
- authenticated logout
- access token validation against live account session state
- trial activation only after email verification
- device registration with plan-based device limit
- device registration blocked until email verification
- device revoke
- control-plane-backed list of issuable VPN nodes
- device-bound VPN access issuance through the control plane
- control-plane metadata propagation into peer metadata
- PostgreSQL persistence through EF Core

## Current API Surface

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/verify-email`
- `POST /api/auth/resend-verification-email`
- `POST /api/auth/logout`
- `GET /api/me`
- `GET /api/devices`
- `POST /api/devices`
- `DELETE /api/devices/{deviceId}`
- `GET /api/sessions`
- `DELETE /api/sessions/{sessionId}`
- `GET /api/access-grants`
- `GET /api/access-grants/nodes`
- `POST /api/access-grants`
- `GET /healthz`

`AccessGrant` is no longer just a passive history row. The product platform can now:

- select a healthy node from the control plane
- issue a device-bound access for an active device
- persist `controlPlaneAccessId`, `peerPublicKey`, and `allowedIps`
- return the generated `.vpn` or `.conf` payload to the caller

## Current Product Assumption

For bootstrap purposes, registration creates a `PendingVerification` account first. After the user confirms the mailbox, the platform activates the default trial subscription from the seeded plan.

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

1. add revoke/rotate flow from `Product Platform` back into `Control Plane`
2. let the desktop client authenticate and request device enrollment directly
3. add billing provider abstraction and webhook handling
4. harden refresh-token persistence and cleanup jobs
5. add audit trail and operator tooling around device revoke and entitlement changes
