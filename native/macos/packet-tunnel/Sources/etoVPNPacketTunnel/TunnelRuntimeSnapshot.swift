import Foundation
import etoVPNMacShared

struct TunnelRuntimeSnapshot {
    let connected: Bool
    let state: RuntimeTunnelState
    let rxBytes: Int64
    let txBytes: Int64
    let latestHandshakeAtUtc: String?
    let engineName: String?
    let interfaceName: String?
    let runtimeConfigurationSummary: String?
    let warnings: [String]
    let lastError: String?

    static func disconnected() -> TunnelRuntimeSnapshot {
        TunnelRuntimeSnapshot(
            connected: false,
            state: .disconnected,
            rxBytes: 0,
            txBytes: 0,
            latestHandshakeAtUtc: nil,
            engineName: nil,
            interfaceName: nil,
            runtimeConfigurationSummary: nil,
            warnings: [],
            lastError: nil)
    }

    func asProviderMessage() -> TunnelProviderMessageStatusResponse {
        TunnelProviderMessageStatusResponse(
            connected: connected,
            state: state,
            rxBytes: rxBytes,
            txBytes: txBytes,
            latestHandshakeAtUtc: latestHandshakeAtUtc,
            engineName: engineName,
            interfaceName: interfaceName,
            runtimeConfigurationSummary: runtimeConfigurationSummary,
            warnings: warnings,
            lastError: lastError)
    }
}
