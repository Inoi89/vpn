import Foundation
import etoVPNMacShared

final class BridgeCommandDispatcher {
    private let coordinator: PacketTunnelCoordinator
    private let statusStore: StatusSnapshotStore

    init(
        coordinator: PacketTunnelCoordinator,
        statusStore: StatusSnapshotStore)
    {
        self.coordinator = coordinator
        self.statusStore = statusStore
    }

    func handleHello(_ payload: HelloRequestPayload) -> RuntimeBridgeSuccessEnvelope<HealthResponsePayload> {
        RuntimeBridgeSuccessEnvelope(
            payload: HealthResponsePayload(
                helperVersion: "0.0.0-scaffold",
                protocolVersion: RuntimeBridgeConstants.protocolVersion,
                socketPath: SocketPathResolver.defaultSocketURL().path,
                capabilities: RuntimeBridgeConstants.capabilities))
    }

    func handleHealth() -> RuntimeBridgeSuccessEnvelope<HealthResponsePayload> {
        RuntimeBridgeSuccessEnvelope(
            payload: HealthResponsePayload(
                helperVersion: "0.0.0-scaffold",
                protocolVersion: RuntimeBridgeConstants.protocolVersion,
                socketPath: SocketPathResolver.defaultSocketURL().path,
                capabilities: RuntimeBridgeConstants.capabilities))
    }

    func handleConfigure(_ payload: ConfigureRequestPayload) -> RuntimeBridgeSuccessEnvelope<StatusResponsePayload> {
        coordinator.stageProfile(payload)
        let snapshot = statusStore.snapshot()
        return RuntimeBridgeSuccessEnvelope(payload: snapshot)
    }

    func handleActivate(_ payload: ActivateRequestPayload) -> RuntimeBridgeSuccessEnvelope<StatusResponsePayload> {
        coordinator.stageProfile(payload)
        coordinator.requestActivation()
        let snapshot = statusStore.snapshot()
        return RuntimeBridgeSuccessEnvelope(payload: snapshot)
    }

    func handleDeactivate(_ payload: DeactivateRequestPayload?) -> RuntimeBridgeSuccessEnvelope<StatusResponsePayload> {
        coordinator.requestDeactivation(profileId: payload?.profileId)
        let snapshot = statusStore.snapshot()
        return RuntimeBridgeSuccessEnvelope(payload: snapshot)
    }

    func handleStatus() -> RuntimeBridgeSuccessEnvelope<StatusResponsePayload> {
        RuntimeBridgeSuccessEnvelope(payload: statusStore.snapshot())
    }

    func handleLogs() -> RuntimeBridgeSuccessEnvelope<LogsResponsePayload> {
        RuntimeBridgeSuccessEnvelope(payload: LogsResponsePayload(entries: []))
    }

    func handleQuit() -> RuntimeBridgeSuccessEnvelope<QuitResponsePayload> {
        RuntimeBridgeSuccessEnvelope(payload: QuitResponsePayload(accepted: true))
    }
}
