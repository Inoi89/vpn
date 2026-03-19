# VPN Control Plane Operational State

Snapshot date: `2026-03-18`

This document captures the current operational state of the VPN control plane workspace in this repository. It is intentionally more concrete than `architecture.md`: it reflects what is actually deployed, what was changed during rollout, and what is still unresolved.

Related baseline design:
- [architecture.md](../architecture.md)
- [docs/operational-state.md](./operational-state.md)
- [docs/mvp-platform-plan.md](./mvp-platform-plan.md)

## 1. What Exists Today

The repository currently contains a split control plane architecture:

- `VpnControlPlane.Api` is the central control plane.
- `VpnNodeAgent` runs on each VPN server as a lightweight stateless agent.
- PostgreSQL is the source of truth for nodes, users, peer configs, sessions, and traffic samples.
- SignalR is used to push near real-time session updates to the UI.
- The React UI is an operator dashboard that is being reshaped for internal-only use.

The design is pull-first:

- The control plane polls agents on a schedule instead of requiring inbound access to each node.
- Agents remain stateless and only read local container/runtime state.
- The system scales operationally better for a small fleet of remote servers.

## 2. Architecture

### 2.1 Control Plane

The control plane is a .NET 8 application with Clean Architecture boundaries:

- `Domain` holds entities and enums.
- `Application` holds CQRS commands and queries.
- `Infrastructure` contains EF Core persistence, polling jobs, and agent client code.
- `Api` hosts controllers, SignalR hubs, auth, Swagger, and startup wiring.

Relevant files:
- [`src/ControlPlane/VpnControlPlane.Api/Program.cs`](../src/ControlPlane/VpnControlPlane.Api/Program.cs)
- [`src/ControlPlane/VpnControlPlane.Api/Controllers/NodesController.cs`](../src/ControlPlane/VpnControlPlane.Api/Controllers/NodesController.cs)
- [`src/ControlPlane/VpnControlPlane.Api/Controllers/DashboardController.cs`](../src/ControlPlane/VpnControlPlane.Api/Controllers/DashboardController.cs)
- [`src/ControlPlane/VpnControlPlane.Api/Hubs/SessionUpdatesHub.cs`](../src/ControlPlane/VpnControlPlane.Api/Hubs/SessionUpdatesHub.cs)
- [`src/ControlPlane/VpnControlPlane.Infrastructure/BackgroundJobs/NodePollingJob.cs`](../src/ControlPlane/VpnControlPlane.Infrastructure/BackgroundJobs/NodePollingJob.cs)

Current control plane responsibilities:

- Register nodes.
- Poll agents.
- Aggregate runtime snapshots into `Node`, `VpnUser`, `PeerConfig`, `Session`, and `TrafficStats`.
- Expose dashboard and node-management endpoints.
- Broadcast state changes over SignalR.

Important runtime wiring:

- JWT auth protects the UI/API.
- SignalR uses the same bearer token.
- Hangfire schedules recurring node polling.
- The API uses a client certificate when calling agents.

### 2.2 Node Agent

`VpnNodeAgent` is a stateless HTTPS service that runs alongside Amnezia/WireGuard on each server.

Relevant files:
- [`src/NodeAgent/VpnNodeAgent/Program.cs`](../src/NodeAgent/VpnNodeAgent/Program.cs)
- [`src/NodeAgent/VpnNodeAgent/Endpoints/AgentEndpoints.cs`](../src/NodeAgent/VpnNodeAgent/Endpoints/AgentEndpoints.cs)
- [`src/NodeAgent/VpnNodeAgent/Services/AgentSnapshotService.cs`](../src/NodeAgent/VpnNodeAgent/Services/AgentSnapshotService.cs)
- [`src/NodeAgent/VpnNodeAgent/Services/AgentAccessService.cs`](../src/NodeAgent/VpnNodeAgent/Services/AgentAccessService.cs)
- [`src/NodeAgent/VpnNodeAgent/Services/WireGuardConfigParser.cs`](../src/NodeAgent/VpnNodeAgent/Services/WireGuardConfigParser.cs)
- [`src/NodeAgent/VpnNodeAgent/Services/WireGuardDumpParser.cs`](../src/NodeAgent/VpnNodeAgent/Services/WireGuardDumpParser.cs)

Node agent responsibilities:

- Execute `wg show` or the equivalent inside the VPN container.
- Parse runtime peer state and traffic counters.
- Read mounted config files such as `wg0.conf`, `clientsTable`, and key files.
- Issue, enable, disable, delete, and re-export access configs.
- Expose a certificate-gated HTTPS endpoint to the control plane.

Operational choices:

- The agent is stateless.
- The agent does not store its own database.
- Docker deployment is the primary mode on the live hosts.
- The agent only reads/writes the mounted Amnezia config directory and synchronizes runtime config back into the container.

### 2.3 Security Model

Current security assumptions:

- Agents are not publicly exposed except for the limited LAN/lab setup currently used.
- The control plane authenticates UI users with JWT.
- The agent requires client certificate validation on its protected endpoints.
- In the current lab rollout, `AllowAnyClientCertificate` is used at Kestrel level and thumbprint authorization is enforced in application code.

This is acceptable for the current internal setup, but it is not a production-hardening endpoint.

## 2.4 Product Boundary

The current control plane is an infrastructure system, not the future public product API.

The next planned layer above it is documented here:

- [docs/mvp-platform-plan.md](./mvp-platform-plan.md)

That future layer should own:

- real customer accounts
- subscription validation
- device enrollment
- anti-sharing policy
- self-service billing

The control plane should remain focused on:

- nodes
- agents
- runtime state
- peer application
- operator rollout

## 3. Current Fleet

Current live nodes known to the control plane as of this snapshot:

| Node | Agent Identifier | Status | Active Sessions | Enabled Peers | Endpoint | Notes |
|---|---|---:|---:|---:|---|---|
| `5.61.37.29` | `amnezia-5-61-37-29` | Healthy | 5 | 11 | `https://5.61.37.29:8443` | Primary operator node. Pinned first in the UI. |
| `37.1.197.163` | `amnezia-node-01` | Healthy | 8 | 13 | `https://37.1.197.163:8443` | Legacy agent identifier retained, but visible name normalized to `Amnezia 37.1.197.163`. |
| `5.61.40.132` | `amnezia-5-61-40-132` | Healthy | 6 | 21 | `https://5.61.40.132:8443` | Large existing node, useful for validating historical configs. |
| `5.61.37.83` | `amnezia-5-61-37-83` | Healthy | 0 | 0 | `https://5.61.37.83:8443` | New Inferno replacement node. Germany location. |
| `5.61.41.204` | `amnezia-5-61-41-204` | Healthy | 0 | 0 | `https://5.61.41.204:8443` | New Inferno replacement node. Germany location. |
| `185.21.10.217` | `amnezia-185-21-10-217` | Healthy | 1 | 5 | `https://185.21.10.217:8443` | Healthy, low activity. |

UI-specific ordering and naming rules currently in effect:

- `5.61.37.29` is treated as the primary node and is pinned to the top of the sidebar.
- Generic names such as `Amnezia Node 01` are normalized in the UI to `Amnezia <ip>` using `agentBaseAddress` or `agentIdentifier` fallback logic.

## 4. Decisions and Rollout Notes

### 4.1 Pull-First Aggregation

We intentionally chose polling over push from the nodes:

- Simpler firewall model.
- No inbound agent exposure from the internet.
- Easier to operate for a handful of remote servers.
- The domain model can later support push or streaming without changing the read model.

### 4.2 Amnezia and WireGuard Compatibility

The agent is not reverse-engineering Amnezia binaries.

Instead, it:

- reads the same config artifacts Amnezia uses,
- parses `wg show` / config files,
- preserves AWG metadata,
- and writes back configs in Amnezia-compatible layouts.

This is a deliberate choice: metadata-driven integration is more robust than trying to emulate every internal client behavior.

### 4.3 Node-Specific Rollout

The current rollout is not homogeneous:

- Most nodes run the agent with `DefaultClientMtu = 1376`.
- DNS on the agent-generated client exports is currently set to `8.8.8.8, 8.8.4.4`.

This matters because config generation behavior has been a moving target during debugging.

### 4.4 Naming Convention

Current naming in the fleet is partially normalized:

- Most nodes are registered as `Amnezia <IP>`.
- `37.1.197.163` still keeps the legacy agent identifier `amnezia-node-01`, but the visible name is now normalized to the IP-based form.

The intended convention is to keep all visible names IP-based and to preserve the operator's main server as a distinct pinned node.

## 5. Known Problems and Risks

### 5.1 The Main Open Problem

The biggest unresolved issue is access-generation compatibility between our panel and Amnezia import/runtime behavior.

Observed pattern before the replacement:

- Amnezia-generated configs could still fail on specific nodes.
- Our generated configs could establish a handshake.
- In some cases, they still failed to carry normal user traffic, or the client reported `connected` while DNS / browsing was broken.

This is not a simple `peer not created` failure. The server often sees the handshake and at least some traffic.

### 5.2 What Was Already Confirmed

We confirmed a few concrete issues and fixes:

- Older panel-generated configs were missing DNS in some cases.
- That caused classic `unknown host` failures.
- We also found that a raw `.conf` export can differ from Amnezia's internal enrollment path in more than one way.
- Raw `.conf` exports now explicitly include `MTU`, because the previous export path silently dropped node-specific MTU overrides.

We also confirmed an important negative result:

- `45.136.49.191` could fail even with configs generated by the stock Amnezia app itself.
- That means the issue on that node was not sufficient evidence of a broken control-plane export path.

### 5.3 Updated Conclusion

The remaining compatibility risk is client/runtime related in general, but the replaced Inferno nodes were specifically a network-location problem, not a peer-generation problem.

Why:

- upstream Amnezia's server-side enrollment flow is basically `append peer -> syncconf -> update clientsTable`;
- our node agent now follows that model closely;
- the old problematic Inferno US/NL nodes were replaced with `5.61.37.83` and `5.61.41.204`;
- after replacement, test accesses issued from the control plane worked on both new nodes;
- that narrows the previous failure mode on `38.180.137.249` / `45.136.49.191` down to path/provider/location characteristics.

### 5.4 Other Operational Risks

- `TrafficStats` is append-only and will grow quickly.
- UI currently uses a lab JWT token baked into the compose setup.
- The agent endpoint is protected by certificates, but the lab Kestrel setup is still permissive at the transport layer.
- The current flow is internal-only and should not be treated as internet-hardened.
- `test123` and similar old configs can remain live on nodes until explicitly deleted from the control plane and re-synced.

### 5.5 Replacement Note

As of `2026-03-18`, the following nodes are intentionally retired from the control plane:

- `38.180.137.249`
- `45.136.49.191`

They were replaced with:

- `5.61.37.83`
- `5.61.41.204`

The control plane database was cleaned so the retired nodes and their orphaned user records are no longer part of the live dashboard.

## 6. Deployment and Verification

### 6.1 Control Plane

Current LAN deployment:

- [`deploy/docker-compose.lan.yml`](../deploy/docker-compose.lan.yml)

What it launches:

- PostgreSQL.
- Control API.
- Static UI.

Common ports:

- UI: `http://192.168.1.2:5080`
- API: `http://192.168.1.2:7001`

### 6.2 Node Agent

Sample deployment files:

- [`deploy/node-agent.compose.sample.yml`](../deploy/node-agent.compose.sample.yml)
- [`deploy/node-agent.docker-mode.yml`](../deploy/node-agent.docker-mode.yml)

Agent rollout pattern used in this workspace:

- publish the agent to `artifacts/node-agent-publish/`;
- package the publish output as a tarball;
- copy the tarball to the node;
- extract it into a temporary directory;
- `docker cp` the files into the running `vpn-node-agent` container;
- restart the agent container;
- verify the live `appsettings.json` inside the container.

### 6.3 Verification

Repeated verification commands used during rollout:

- `dotnet build VpnControlPlane.sln`
- `dotnet test VpnControlPlane.sln`
- `npm run build` in `frontend/control-plane-ui`

Node-level sanity checks:

- `wg show wg0`
- container file reads under `/opt/amnezia/awg`
- `clientsTable` contents
- agent `/healthz`
- control plane `/api/dashboard`

## 7. Useful Endpoints

### Control Plane

- `GET /healthz`
- `GET /api/dashboard`
- `GET /api/nodes`
- `GET /api/nodes/{nodeId}/sessions`
- `POST /api/nodes/register`
- `POST /api/nodes/{nodeId}/accesses`
- `POST /api/nodes/{nodeId}/accesses/{userId}/state`
- `DELETE /api/nodes/{nodeId}/accesses/{userId}`
- `GET /api/nodes/{nodeId}/accesses/{userId}/config`
- `GET /hubs/sessions`

### Node Agent

- `GET /healthz`
- `GET /v1/agent/snapshot`
- `POST /accesses/issue`
- `POST /accesses/state`
- `POST /accesses/delete`
- `POST /accesses/config`

## 8. Useful Artifacts

Current workspace artifacts and committed operator-facing files:

- [`frontend/control-plane-ui/nginx.conf`](../frontend/control-plane-ui/nginx.conf)
- [`src/ControlPlane/VpnControlPlane.Api/VpnControlPlane.Api.http`](../src/ControlPlane/VpnControlPlane.Api/VpnControlPlane.Api.http)
- [`src/NodeAgent/VpnNodeAgent/VpnNodeAgent.http`](../src/NodeAgent/VpnNodeAgent/VpnNodeAgent.http)
- [`docs/operational-state.md`](./operational-state.md)

## 9. Phase 2 Direction

The next product step is not more backend generation logic.

The intended direction is:

- make the UI feel like a native internal admin app;
- support "add server from config file";
- support "connect" and manage lifecycle;
- remove or de-emphasize config generation from the main workflow;
- keep the Amnezia fleet as the transport layer for now.

This is the right next layer because the transport is already operational. The unresolved part is the user-facing import/enrollment experience.

See also:
- [docs/desktop-client-direction.md](./desktop-client-direction.md)
