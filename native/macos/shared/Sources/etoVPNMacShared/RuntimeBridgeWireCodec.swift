import Foundation

public enum RuntimeBridgeRequestPayload {
    case hello(HelloRequestPayload)
    case configure(ConfigureRequestPayload)
    case activate(ActivateRequestPayload)
    case deactivate(DeactivateRequestPayload?)
    case empty
}

public struct RuntimeBridgeRequest {
    public let id: String
    public let command: RuntimeBridgeCommand
    public let payload: RuntimeBridgeRequestPayload

    public init(id: String, command: RuntimeBridgeCommand, payload: RuntimeBridgeRequestPayload) {
        self.id = id
        self.command = command
        self.payload = payload
    }
}

public enum RuntimeBridgeWireCodecError: Error, LocalizedError, CustomStringConvertible {
    case invalidEnvelope
    case invalidType(String)
    case missingField(String)
    case invalidPayload(RuntimeBridgeCommand)
    case unsupportedCommand(String)
    case invalidUTF8

    public var description: String {
        switch self {
        case .invalidEnvelope:
            return "The bridge request envelope was not a JSON object."
        case .invalidType(let type):
            return "The bridge request type '\(type)' is not supported."
        case .missingField(let field):
            return "The bridge request is missing required field '\(field)'."
        case .invalidPayload(let command):
            return "The bridge request payload for '\(command.rawValue)' is invalid."
        case .unsupportedCommand(let command):
            return "The bridge command '\(command)' is not supported."
        case .invalidUTF8:
            return "The bridge response could not be encoded as UTF-8."
        }
    }

    public var errorDescription: String? {
        description
    }
}

public enum RuntimeBridgeWireCodec {
    public static func decodeRequestLine(_ line: String) throws -> RuntimeBridgeRequest {
        let data = Data(line.utf8)
        let object = try JSONSerialization.jsonObject(with: data, options: [])
        guard let envelope = object as? [String: Any] else {
            throw RuntimeBridgeWireCodecError.invalidEnvelope
        }

        let id = try requiredStringValue(for: "id", in: envelope)
        let type = try requiredStringValue(for: "type", in: envelope)
        guard type == "request" else {
            throw RuntimeBridgeWireCodecError.invalidType(type)
        }

        let commandRaw = try requiredStringValue(for: "command", in: envelope)
        guard let command = RuntimeBridgeCommand(rawValue: commandRaw) else {
            throw RuntimeBridgeWireCodecError.unsupportedCommand(commandRaw)
        }

        let payload = envelope["payload"]
        switch command {
        case .hello:
            return RuntimeBridgeRequest(
                id: id,
                command: command,
                payload: .hello(try decodeRequiredPayload(payload, as: HelloRequestPayload.self, command: command)))
        case .configure:
            return RuntimeBridgeRequest(
                id: id,
                command: command,
                payload: .configure(try decodeRequiredPayload(payload, as: ConfigureRequestPayload.self, command: command)))
        case .activate:
            return RuntimeBridgeRequest(
                id: id,
                command: command,
                payload: .activate(try decodeRequiredPayload(payload, as: ActivateRequestPayload.self, command: command)))
        case .deactivate:
            return RuntimeBridgeRequest(
                id: id,
                command: command,
                payload: .deactivate(try decodeOptionalPayload(
                    payload,
                    as: DeactivateRequestPayload.self,
                    command: command)))
        case .health, .status, .logs, .quit:
            return RuntimeBridgeRequest(id: id, command: command, payload: .empty)
        }
    }

    public static func encodeLine<T: Encodable>(_ value: T) throws -> String {
        let data = try makeJSONEncoder().encode(value)
        guard let line = String(data: data, encoding: .utf8) else {
            throw RuntimeBridgeWireCodecError.invalidUTF8
        }

        return line
    }

    public static func encodeFailureLine(
        id: String?,
        code: String,
        message: String,
        details: String? = nil) -> String
    {
        let envelope = RuntimeBridgeFailureEnvelope(
            id: id,
            error: BridgeErrorPayload(code: code, message: message, details: details))

        return (try? encodeLine(envelope))
            ?? #"{"type":"response","ok":false,"error":{"code":"internal_error","message":"Unable to encode bridge error response.","details":null}}"#
    }

    private static func requiredStringValue(for key: String, in envelope: [String: Any]) throws -> String {
        guard let value = envelope[key] as? String, !value.isEmpty else {
            throw RuntimeBridgeWireCodecError.missingField(key)
        }

        return value
    }

    private static func decodeRequiredPayload<T: Decodable>(
        _ payload: Any?,
        as _: T.Type,
        command: RuntimeBridgeCommand) throws -> T
    {
        guard let data = try payloadData(from: payload, command: command) else {
            throw RuntimeBridgeWireCodecError.invalidPayload(command)
        }

        return try makeJSONDecoder().decode(T.self, from: data)
    }

    private static func decodeOptionalPayload<T: Decodable>(
        _ payload: Any?,
        as _: T.Type,
        command: RuntimeBridgeCommand) throws -> T?
    {
        guard let data = try payloadData(from: payload, command: command) else {
            return nil
        }

        return try makeJSONDecoder().decode(T.self, from: data)
    }

    private static func payloadData(from payload: Any?, command: RuntimeBridgeCommand) throws -> Data? {
        guard let payload else {
            return nil
        }

        if payload is NSNull {
            return nil
        }

        guard JSONSerialization.isValidJSONObject(payload) else {
            throw RuntimeBridgeWireCodecError.invalidPayload(command)
        }

        return try JSONSerialization.data(withJSONObject: payload, options: [])
    }

    private static func makeJSONEncoder() -> JSONEncoder {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys]
        return encoder
    }

    private static func makeJSONDecoder() -> JSONDecoder {
        JSONDecoder()
    }
}
