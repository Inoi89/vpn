import Foundation
import etoVPNMacShared

final class BridgeCommandDispatcher {
    private let socketPath: URL
    private let coordinator: PacketTunnelCoordinator
    private let statusStore: StatusSnapshotStore
    private let diagnosticsStore: ProviderDiagnosticsStore

    init(
        socketPath: URL,
        coordinator: PacketTunnelCoordinator,
        statusStore: StatusSnapshotStore,
        diagnosticsStore: ProviderDiagnosticsStore)
    {
        self.socketPath = socketPath
        self.coordinator = coordinator
        self.statusStore = statusStore
        self.diagnosticsStore = diagnosticsStore
    }

    func dispatchLine(_ request: RuntimeBridgeRequest) -> String {
        do {
            switch (request.command, request.payload) {
            case (.hello, .hello(let payload)):
                return try RuntimeBridgeWireCodec.encodeLine(handleHello(requestId: request.id, payload: payload))
            case (.health, .empty):
                return try RuntimeBridgeWireCodec.encodeLine(handleHealth(requestId: request.id))
            case (.configure, .configure(let payload)):
                return try RuntimeBridgeWireCodec.encodeLine(handleConfigure(requestId: request.id, payload: payload))
            case (.activate, .activate(let payload)):
                return try RuntimeBridgeWireCodec.encodeLine(handleActivate(requestId: request.id, payload: payload))
            case (.deactivate, .deactivate(let payload)):
                return try RuntimeBridgeWireCodec.encodeLine(handleDeactivate(requestId: request.id, payload: payload))
            case (.status, .empty):
                return try RuntimeBridgeWireCodec.encodeLine(handleStatus(requestId: request.id))
            case (.logs, .empty):
                return try RuntimeBridgeWireCodec.encodeLine(handleLogs(requestId: request.id))
            case (.quit, .empty):
                return try RuntimeBridgeWireCodec.encodeLine(handleQuit(requestId: request.id))
            default:
                return RuntimeBridgeWireCodec.encodeFailureLine(
                    id: request.id,
                    code: "invalid_request",
                    message: "The request envelope did not match the declared command.",
                    details: "Command: \(request.command.rawValue)")
            }
        } catch {
            return RuntimeBridgeWireCodec.encodeFailureLine(
                id: request.id,
                code: "internal_error",
                message: "The bridge could not encode a response.",
                details: error.localizedDescription)
        }
    }

    func handleHello(
        requestId: String,
        payload: HelloRequestPayload) -> RuntimeBridgeSuccessEnvelope<HealthResponsePayload>
    {
        _ = payload
        return RuntimeBridgeSuccessEnvelope(
            id: requestId,
            payload: HealthResponsePayload(
                helperVersion: "0.0.0-scaffold",
                protocolVersion: RuntimeBridgeConstants.protocolVersion,
                socketPath: socketPath.path,
                capabilities: RuntimeBridgeConstants.capabilities))
    }

    func handleHealth(requestId: String) -> RuntimeBridgeSuccessEnvelope<HealthResponsePayload> {
        RuntimeBridgeSuccessEnvelope(
            id: requestId,
            payload: HealthResponsePayload(
                helperVersion: "0.0.0-scaffold",
                protocolVersion: RuntimeBridgeConstants.protocolVersion,
                socketPath: socketPath.path,
                capabilities: RuntimeBridgeConstants.capabilities))
    }

    func handleConfigure(
        requestId: String,
        payload: ConfigureRequestPayload) -> RuntimeBridgeSuccessEnvelope<StatusResponsePayload>
    {
        coordinator.stageProfile(payload)
        return RuntimeBridgeSuccessEnvelope(id: requestId, payload: statusStore.snapshot())
    }

    func handleActivate(
        requestId: String,
        payload: ActivateRequestPayload) -> RuntimeBridgeSuccessEnvelope<StatusResponsePayload>
    {
        coordinator.stageProfile(payload)
        coordinator.requestActivation()
        return RuntimeBridgeSuccessEnvelope(id: requestId, payload: statusStore.snapshot())
    }

    func handleDeactivate(
        requestId: String,
        payload: DeactivateRequestPayload?) -> RuntimeBridgeSuccessEnvelope<StatusResponsePayload>
    {
        coordinator.requestDeactivation(profileId: payload?.profileId)
        return RuntimeBridgeSuccessEnvelope(id: requestId, payload: statusStore.snapshot())
    }

    func handleStatus(requestId: String) -> RuntimeBridgeSuccessEnvelope<StatusResponsePayload> {
        let snapshot = statusStore.snapshot()
        let diagnosticsWarnings = diagnosticsStore.warningLines()
        guard !diagnosticsWarnings.isEmpty else {
            return RuntimeBridgeSuccessEnvelope(id: requestId, payload: snapshot)
        }

        return RuntimeBridgeSuccessEnvelope(
            id: requestId,
            payload: StatusResponsePayload(
                connected: snapshot.connected,
                state: snapshot.state,
                profileId: snapshot.profileId,
                profileName: snapshot.profileName,
                serverEndpoint: snapshot.serverEndpoint,
                deviceIpv4Address: snapshot.deviceIpv4Address,
                deviceIpv6Address: snapshot.deviceIpv6Address,
                dns: snapshot.dns,
                mtu: snapshot.mtu,
                allowedIps: snapshot.allowedIps,
                routes: snapshot.routes,
                rxBytes: snapshot.rxBytes,
                txBytes: snapshot.txBytes,
                latestHandshakeAtUtc: snapshot.latestHandshakeAtUtc,
                warnings: snapshot.warnings + diagnosticsWarnings,
                lastError: snapshot.lastError))
    }

    func handleLogs(requestId: String) -> RuntimeBridgeSuccessEnvelope<LogsResponsePayload> {
        RuntimeBridgeSuccessEnvelope(
            id: requestId,
            payload: LogsResponsePayload(entries: coordinator.requestLogs()))
    }

    func handleQuit(requestId: String) -> RuntimeBridgeSuccessEnvelope<QuitResponsePayload> {
        RuntimeBridgeSuccessEnvelope(id: requestId, payload: QuitResponsePayload(accepted: true))
    }
}
