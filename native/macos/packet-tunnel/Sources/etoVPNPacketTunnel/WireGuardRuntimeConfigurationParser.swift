import Foundation

enum WireGuardRuntimeConfigurationParser {
    struct ParsedStatus {
        let rxBytes: Int64
        let txBytes: Int64
        let latestHandshakeAtUtc: String?
        let redactedConfiguration: String?
    }

    private static let dateFormatter: ISO8601DateFormatter = {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime]
        return formatter
    }()

    static func parse(_ runtimeConfiguration: String?) -> ParsedStatus {
        guard let runtimeConfiguration,
              !runtimeConfiguration.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        else {
            return ParsedStatus(
                rxBytes: 0,
                txBytes: 0,
                latestHandshakeAtUtc: nil,
                redactedConfiguration: nil)
        }

        let values = runtimeConfiguration
            .split(separator: "\n", omittingEmptySubsequences: true)
            .reduce(into: [String: String]()) { dictionary, line in
                guard let separatorIndex = line.firstIndex(of: "=") else {
                    return
                }

                let key = String(line[..<separatorIndex])
                let value = String(line[line.index(after: separatorIndex)...])
                dictionary[key] = value
            }

        let latestHandshakeAtUtc = values["last_handshake_time_sec"]
            .flatMap(Int64.init)
            .flatMap { $0 > 0 ? dateFormatter.string(from: Date(timeIntervalSince1970: TimeInterval($0))) : nil }

        return ParsedStatus(
            rxBytes: values["rx_bytes"].flatMap(Int64.init) ?? 0,
            txBytes: values["tx_bytes"].flatMap(Int64.init) ?? 0,
            latestHandshakeAtUtc: latestHandshakeAtUtc,
            redactedConfiguration: redact(runtimeConfiguration))
    }

    private static func redact(_ configuration: String) -> String {
        configuration
            .split(separator: "\n", omittingEmptySubsequences: false)
            .map { line -> String in
                let text = String(line)
                if text.hasPrefix("private_key=") {
                    return "private_key=***"
                }

                if text.hasPrefix("public_key=") {
                    return "public_key=***"
                }

                if text.hasPrefix("preshared_key=") {
                    return "preshared_key=***"
                }

                return text
            }
            .joined(separator: "\n")
    }
}
