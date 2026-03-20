import Foundation

public enum RuntimeBridgeCommand: String, Codable {
    case hello
    case health
    case configure
    case activate
    case deactivate
    case status
    case logs
    case quit
}

public enum RuntimeTunnelState: String, Codable {
    case disconnected
    case connecting
    case connected
    case degraded
    case failed
}
