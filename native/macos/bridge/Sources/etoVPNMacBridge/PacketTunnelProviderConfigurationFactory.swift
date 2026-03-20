import Foundation
import etoVPNMacShared

enum PacketTunnelProviderConfigurationFactory {
    static func makeProviderConfiguration(for profile: TunnelProfilePayload) throws -> [String: Any] {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys]
        let wireGuardPayload = try WireGuardProviderConfiguration.from(profile: profile)
        let payload = try encoder.encode(wireGuardPayload)

        return [
            RuntimeBridgeConstants.providerProfilePayloadKey: payload,
            RuntimeBridgeConstants.providerProfileIdKey: profile.profileId,
            RuntimeBridgeConstants.providerProfileNameKey: profile.profileName,
            RuntimeBridgeConstants.providerConfigFormatKey: profile.managedProfile?.configFormat ?? profile.sourceFormat ?? profile.tunnelConfig.format
        ]
    }
}
