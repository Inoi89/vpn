# MVP Platform Plan

Snapshot date: `2026-03-19`

This document describes the next product layer above the already working VPN control plane and desktop client.

The current stack already solves:

- node telemetry
- access issuance to specific nodes
- desktop import and connect
- desktop self-update

It does not yet solve:

- real end-user identity
- entitlement and subscription control
- device limits
- anti-sharing enforcement
- self-service billing
- centralized rollout of node-level config changes across the fleet

This document is the plan for those missing systems.

Implementation seed now exists here:

- [VpnProductPlatform.sln](/c:/Users/rrese/source/repos/vpn/VpnProductPlatform.sln)
- [product-platform-architecture.md](/c:/Users/rrese/source/repos/vpn/docs/product-platform-architecture.md)
- [product-platform-deployment.md](/c:/Users/rrese/source/repos/vpn/docs/product-platform-deployment.md)

## 1. Product Goal

Target MVP behavior:

1. A user has an account.
2. A user pays for access.
3. A user installs the desktop client.
4. The desktop client authenticates against our platform.
5. The platform issues a device-bound VPN access for that specific device.
6. The user connects with one click.
7. The platform can revoke, rotate, expire, or reassign access centrally.
8. Operators can roll out fleet-wide node changes without SSH-ing into each host manually.

Important product boundary:

- VPN traffic should still go directly from client to VPN nodes.
- The central platform should authorize, issue, rotate, and revoke access.
- We do not need an inline traffic proxy in the middle of the VPN path.

## 2. What Exists Today

The existing repository already contains:

- a working control plane API
- working node agents on live servers
- access issuance in `.conf` and `.vpn`
- working Windows desktop client with bundled AmneziaWG runtime
- hosted client update origin

This is enough to serve as the technical base.

It is not enough to serve as a user-facing VPN product.

## 3. Main Missing Systems

### 3.1 Identity Layer

Today, `VpnUser` is effectively a technical record around issued peer configs, not a true product identity.

We are missing:

- account registration
- login
- password reset
- password reset flow
- account status
- owned devices
- owned entitlements

What now exists in seed form:

- account registration and login
- JWT access token issuance
- refresh token rotation
- account session list and revoke

### 3.2 Entitlement Layer

We currently do not have:

- plans
- subscriptions
- active/inactive access windows
- trial logic
- grace periods
- cancellation behavior
- device limits per plan

### 3.3 Anti-Sharing Layer

Raw static WireGuard/Amnezia configs are not enough to stop sharing.

If one config is sent to many people:

- they can reuse the same key
- endpoint ownership will jump between users
- one user may evict another
- this is detectable, but not cleanly preventable without a platform layer

So the anti-sharing problem must be solved through:

- per-device issuance
- device count limits
- enrollment flow
- anomaly detection
- revocation

This has an operator-facing consequence:

- one account can no longer be treated as one VPN key;
- the control plane must show the concrete access/peer that belongs to a device;
- old manually issued keys will remain partially anonymous until they are reissued or migrated.

### 3.4 Fleet Rollout Layer

Right now we can manage peers on individual nodes, but we do not have:

- desired state for node config
- rollout campaigns
- config versioning
- drift detection
- rollback
- coordinated config changes across all nodes

## 4. Target Architecture

The next product architecture should consist of five layers.

### 4.1 Public Web Layer

Purpose:

- landing site
- personal account
- login/register
- billing UI
- device management UI
- download page for desktop client

Current work already started:

- [frontend/product-platform-web](/c:/Users/rrese/source/repos/vpn/frontend/product-platform-web)

Suggested shape:

- separate website or frontend app
- backed by a new public product API

### 4.2 Product API

Purpose:

- account auth
- subscription checks
- device registration
- issuance authorization
- enrollment tokens
- plan enforcement
- billing webhooks

Current API hardening now includes:

- refresh tokens
- account sessions
- logout
- access token validation against live session state
- CORS hook for separate frontend origin
- forwarded headers for proxy deployments

This is the missing "server-side validation" layer.

It should sit above the current control plane, not inside the desktop client.

### 4.3 Control Plane

The current control plane should remain the infrastructure control system.

It should own:

- nodes
- node agents
- runtime state
- peer configs on nodes
- rollout jobs
- operator dashboard

It should stop being the place where product identity is invented ad hoc.

At the same time, it should consume product identity metadata for observability.

Minimum operator-facing data per access should be:

- node
- issued tunnel IP
- public key
- access status
- account email/display name when present
- device name/platform when present
- fingerprint or client version when present

### 4.4 Desktop Client

The desktop client should become:

- login-aware
- device-aware
- profile-light
- one-click connect

The preferred long-term flow is:

- desktop app authenticates
- desktop app requests or refreshes its device access
- desktop app receives device-bound config
- desktop app connects

This is better than asking end users to move raw files around forever.

### 4.5 Node Agents

Node agents should stay stateless.

But they should also evolve to support:

- declarative node settings apply
- config version apply
- dry-run or plan
- rollout acknowledgement

## 5. Required Domain Model

The current model is missing a product identity graph.

The next domain model should introduce at least the following entities.

### 5.1 Account

Represents the real customer identity.

Fields:

- `Id`
- `Email`
- `PasswordHash` or external identity reference
- `Status`
- `CreatedAtUtc`
- `LastLoginAtUtc`

### 5.2 Device

Represents a specific enrolled client installation.

Fields:

- `Id`
- `AccountId`
- `DeviceName`
- `Platform`
- `ClientVersion`
- `PublicKey`
- `Fingerprint`
- `Status`
- `LastSeenAtUtc`

### 5.3 SubscriptionPlan

Represents a plan definition.

Fields:

- `Id`
- `Name`
- `MaxDevices`
- `MaxConcurrentSessions`
- `NodePoolPolicy`
- `Price`
- `BillingPeriod`

### 5.4 Subscription

Represents the actual paid entitlement.

Fields:

- `Id`
- `AccountId`
- `PlanId`
- `Status`
- `StartsAtUtc`
- `EndsAtUtc`
- `GraceEndsAtUtc`

### 5.5 Payment

Represents provider-side payment facts.

Fields:

- `Id`
- `AccountId`
- `SubscriptionId`
- `Provider`
- `ProviderPaymentId`
- `Amount`
- `Currency`
- `Status`
- `CapturedAtUtc`

### 5.6 AccessGrant

Represents a device-bound permission to use VPN.

Fields:

- `Id`
- `AccountId`
- `DeviceId`
- `NodeId`
- `PeerPublicKey`
- `ConfigFormat`
- `Status`
- `IssuedAtUtc`
- `RevokedAtUtc`
- `ExpiresAtUtc`

### 5.7 EnrollmentToken

Represents a short-lived token allowing the desktop client to enroll or refresh access.

Fields:

- `Id`
- `AccountId`
- `DeviceId`
- `Purpose`
- `ExpiresAtUtc`
- `UsedAtUtc`

### 5.8 NodeProfile

Represents declarative configuration policy for a node or group of nodes.

Fields:

- `Id`
- `Name`
- `DnsPrimary`
- `DnsSecondary`
- `ClientMtu`
- `AwgDefaults`
- `PortPolicy`
- `ExportDefaults`

### 5.9 RolloutJob

Represents a fleet-wide config rollout.

Fields:

- `Id`
- `NodeProfileId`
- `TargetNodeIds`
- `Version`
- `Status`
- `StartedAtUtc`
- `CompletedAtUtc`

## 6. Key Product Flows

### 6.1 Registration and Purchase

1. User opens the site.
2. User creates account.
3. User buys a plan.
4. Payment webhook activates subscription.
5. User sees active entitlement in personal cabinet.

### 6.2 First Desktop Enrollment

1. User downloads desktop client.
2. User logs in.
3. Client registers as a new `Device`.
4. Product API checks:
   - subscription active
   - device count not exceeded
   - account not blocked
5. Product API asks control plane to issue a device-bound access on an eligible node.
6. Desktop client receives config and stores it locally.
7. Client connects.

Current implementation state:

- `Account`, `Device`, `Subscription`, and `AccessGrant` are already in place.
- email verification and device registration already work.
- `Product Platform` can now call `Control Plane` to issue a device-bound access on a healthy node.
- the remaining gap is teaching the desktop client to authenticate and consume this flow directly instead of importing files manually.

### 6.3 Reconnect

1. Client starts.
2. Client restores local profile.
3. Client optionally refreshes device entitlement.
4. If access is still valid, user connects with one click.

### 6.4 Revoke Device

1. User or operator revokes a device.
2. Product API marks device inactive.
3. Control plane disables or deletes corresponding peer config.
4. Client fails re-auth or reconnect.

### 6.5 Anti-Sharing Detection

Initial MVP does not need impossible cryptographic perfection.

It needs pragmatic controls:

- one access grant per device
- device limit per subscription
- optional concurrent session limit
- endpoint churn detection
- country or ASN anomaly detection
- frequent endpoint replacement alerts
- manual operator revoke

### 6.6 Fleet Rollout

1. Operator edits a `NodeProfile`.
2. Control plane creates `RolloutJob`.
3. Target nodes receive desired config version.
4. Agents apply config safely.
5. Control plane verifies success per node.
6. Failed nodes are reported and can be rolled back.

## 7. Anti-Sharing Strategy

This needs to be explicit because it is one of the most important missing pieces.

### 7.1 What Will Not Work

These are not sufficient:

- only naming keys nicely
- only watching session tables manually
- only checking handshake existence
- relying on "one config should not be shared"

### 7.2 MVP-Grade Enforcement

For MVP, we should implement:

- each device receives its own config
- account plan has `MaxDevices`
- each config is tied to one `Device`
- control plane knows which node and peer belong to that device
- if device is removed, peer is revoked

### 7.3 Optional Stronger Controls Later

Later we can add:

- signed enrollment assertions
- short-lived rotating access packages
- per-device refresh tokens
- mandatory periodic revalidation
- anomaly scoring and automatic suspension

## 8. Billing and Personal Cabinet

Yes, this does imply a website with a personal cabinet.

At minimum, the personal cabinet must support:

- sign in
- current plan
- billing status
- payment history
- devices list
- revoke device
- download desktop app

It does not need full marketing complexity for MVP.

The cabinet is a product surface, not an operator dashboard.

## 9. Fleet Configuration Service

This is a separate problem from billing and identity.

We need an internal mechanism to update all nodes when we change platform-level VPN defaults.

Examples:

- new default DNS
- new MTU
- updated AWG defaults
- changed export behavior
- changed listen port policy

The correct design is:

- desired state stored centrally
- node profile versioning
- controlled rollout
- verification after apply

This should not rely on ad hoc SSH changes.

## 10. Recommended Delivery Order

The next work should be delivered in this order.

### Phase 1: Identity and Device Model

Build:

- `Account`
- `Device`
- `SubscriptionPlan`
- `Subscription`
- login and auth
- refresh tokens and account sessions

Result:

- the platform knows who the real user is
- the desktop client can log in

### Phase 2: Device-Bound Access Issuance

Build:

- enrollment API
- device registration
- access grant issuance
- revoke device

Result:

- every installation gets its own access
- key sharing becomes observable and limitable

### Phase 3: Personal Cabinet and Billing

Build:

- billing provider integration
- subscription activation
- plan limits
- device management UI

Result:

- the product has self-service payments and account lifecycle

### Phase 4: Fleet Rollout Service

Build:

- node profiles
- rollout jobs
- desired-state apply on node agents
- rollback

Result:

- infrastructure changes become centrally manageable

### Phase 5: Policy Enforcement and Hardening

Build:

- anomaly detection
- automated revoke or suspension rules
- audit trail
- better security around tokens and client auth

## 11. Concrete MVP APIs

The future platform layer likely needs at least these endpoints.

### Auth

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`

### Account

- `GET /api/me`
- `GET /api/me/subscription`
- `GET /api/me/devices`
- `DELETE /api/me/devices/{deviceId}`

### Enrollment

- `POST /api/enrollment/start`
- `POST /api/enrollment/complete`
- `POST /api/enrollment/refresh-access`

### Billing

- `POST /api/billing/checkout`
- `POST /api/billing/webhooks/provider`
- `GET /api/billing/history`

### Operator Rollout

- `GET /api/node-profiles`
- `POST /api/node-profiles`
- `POST /api/rollouts`
- `GET /api/rollouts/{rolloutId}`

## 12. Practical Recommendation

Do not try to solve all of this by stretching the current `VpnUser` model.

That would create a confused domain where:

- technical peers
- customer accounts
- devices
- subscriptions

all collapse into one object.

Instead:

- keep control plane infrastructure-focused
- add a product API above it
- make the desktop client talk to the product API for identity and enrollment
- let the product API instruct the control plane when access should exist

## 13. Immediate Next Step

The next concrete implementation step should be:

1. teach the desktop client to authenticate and request device enrollment
2. add revoke and rotate flow from `Product Platform` into `Control Plane`
3. expose the issued device-bound access cleanly in the personal cabinet
4. only after that move to billing provider integration

This order gives the fastest path to a real MVP instead of a collection of infrastructure tools.
