import Foundation
import etoVPNMacShared

final class ProviderDiagnosticsStore {
    private let refreshInterval: TimeInterval = 10
    private var cachedLogs: [String] = []
    private var cachedEngineName: String?
    private var cachedInterfaceName: String?
    private var cachedRuntimeConfigurationSummary: String?
    private var lastRefreshAt: Date?

    func update(from status: TunnelProviderMessageStatusResponse) {
        cachedEngineName = status.engineName ?? cachedEngineName
        cachedInterfaceName = status.interfaceName ?? cachedInterfaceName
        cachedRuntimeConfigurationSummary = status.runtimeConfigurationSummary ?? cachedRuntimeConfigurationSummary
    }

    func update(
        logs: [String]?,
        runtimeConfigurationSummary: String?,
        engineName: String?,
        interfaceName: String?)
    {
        if let logs {
            cachedLogs = logs
        }

        if let runtimeConfigurationSummary {
            cachedRuntimeConfigurationSummary = runtimeConfigurationSummary
        }

        if let engineName {
            cachedEngineName = engineName
        }

        if let interfaceName {
            cachedInterfaceName = interfaceName
        }
    }

    func markRefreshed() {
        lastRefreshAt = Date()
    }

    func clear() {
        cachedLogs = []
        cachedEngineName = nil
        cachedInterfaceName = nil
        cachedRuntimeConfigurationSummary = nil
        lastRefreshAt = nil
    }

    func logs() -> [String] {
        if cachedLogs.isEmpty {
            return ["Provider logs are not cached yet."]
        }

        return cachedLogs
    }

    func hasLogs() -> Bool {
        !cachedLogs.isEmpty
    }

    func warningLines() -> [String] {
        var warnings: [String] = []

        if let cachedEngineName {
            warnings.append("Provider engine: \(cachedEngineName)")
        }

        if let cachedInterfaceName {
            warnings.append("Provider interface: \(cachedInterfaceName)")
        }

        if let cachedRuntimeConfigurationSummary {
            warnings.append("Provider runtime configuration: \(cachedRuntimeConfigurationSummary)")
        }

        return warnings
    }

    func shouldRefresh(now: Date = Date()) -> Bool {
        guard let lastRefreshAt else {
            return true
        }

        return now.timeIntervalSince(lastRefreshAt) >= refreshInterval
    }
}
