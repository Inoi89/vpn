# Product Cabinet Web

Snapshot date: `2026-03-19`

This is the user-facing cabinet for `VpnProductPlatform`.

It is intentionally separate from the operator control plane.

## Purpose

The cabinet is responsible for:

- registration
- login
- account overview
- subscription summary
- device list and revoke
- session list and revoke

It is not responsible for:

- node polling
- peer issuance
- fleet rollout
- operator tooling

## Frontend Location

- [frontend/product-platform-web](/c:/Users/rrese/source/repos/vpn/frontend/product-platform-web)

## Implemented UI Scope

- anonymous login/register screen
- authenticated dashboard
- account summary
- subscription summary
- devices table
- sessions table
- revoke actions for devices and sessions
- Russian UI labels
- flat/simple visual language

## API Dependencies

The cabinet currently uses:

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

Auth notes:

- access tokens now carry a session id claim
- revoked or expired sessions are rejected by the API middleware on every request
- `GET /api/access-grants`

## API Gaps

The cabinet does not yet have a dedicated endpoint for:

- active VPN access grant summary per user/device
- enrollment start and completion
- password reset
- profile edits
- payment state

For now, the UI surfaces:

- subscription summary from `GET /api/me`
- device inventory from `GET /api/devices`
- session inventory from `GET /api/sessions`
- access grant history is available from `GET /api/access-grants` for the next cabinet slice

## Deployment Assumptions

The frontend is designed to be deployed independently from the API.

Recommended deployment shape:

- frontend on `5.61.37.29`
- API on `api.etojesim.com`
- CORS enabled on the API for the frontend origin
- forwarded headers enabled behind nginx/reverse proxy

## Build

```powershell
cd frontend/product-platform-web
npm install
npm run build
```

## Open Tasks

1. wire the frontend to a stable production API URL
2. add enrollment flow
3. add payment and billing screens
4. add password reset
