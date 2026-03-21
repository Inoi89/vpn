#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="Debug"
RUNTIME_IDENTIFIER="osx-arm64"
OUTPUT_ROOT="artifacts/macos-native"
DERIVED_DATA_PATH=""
SKIP_GENERATE="0"
DEVELOPMENT_TEAM="${DEVELOPMENT_TEAM:-}"
CODE_SIGN_STYLE="${CODE_SIGN_STYLE:-Automatic}"
ALLOW_PROVISIONING_UPDATES="${ALLOW_PROVISIONING_UPDATES:-1}"
AMNEZIAWG_APPLE_REF="${AMNEZIAWG_APPLE_REF:-cf63135331d33b2a684cca5a2b213d851cb79e77}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --configuration)
      CONFIGURATION="$2"
      shift 2
      ;;
    --runtime)
      RUNTIME_IDENTIFIER="$2"
      shift 2
      ;;
    --output-root)
      OUTPUT_ROOT="$2"
      shift 2
      ;;
    --derived-data)
      DERIVED_DATA_PATH="$2"
      shift 2
      ;;
    --skip-generate)
      SKIP_GENERATE="1"
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "native/macos/build-native.sh must be run on macOS." >&2
  exit 1
fi

if ! command -v xcodebuild >/dev/null 2>&1; then
  echo "xcodebuild is required but not installed." >&2
  echo "Install Xcode and Xcode Command Line Tools first." >&2
  exit 1
fi

if ! command -v xcodegen >/dev/null 2>&1; then
  echo "xcodegen is required but not installed." >&2
  echo "Install it with: brew install xcodegen" >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PROJECT_SPEC="${SCRIPT_DIR}/project.yml"
PROJECT_PATH="${SCRIPT_DIR}/etoVPNMac.xcodeproj"
OUTPUT_DIR="${REPO_ROOT}/${OUTPUT_ROOT}/${RUNTIME_IDENTIFIER}"
UPSTREAM_ROOT="${REPO_ROOT}/.research/amnezia-client"
WG_APPLE_DIR="${UPSTREAM_ROOT}/client/3rd/amneziawg-apple"
WG_APPLE_WIREGUARD_ADAPTER="${WG_APPLE_DIR}/Sources/WireGuardKit/WireGuardAdapter.swift"
WIREGUARD_GO_DIR="${WG_APPLE_DIR}/Sources/WireGuardKitGo"
WIREGUARD_GENERATED_DIR="${SCRIPT_DIR}/.generated/wireguard-go"
WIREGUARD_GO_TEMP_DIR="${SCRIPT_DIR}/.generated/.tmp/wireguard-go"
PREBUILT_WIREGUARD_LIB="${UPSTREAM_ROOT}/client/3rd-prebuilt/3rd-prebuilt/wireguard/macos/universal2/libwg-go.a"
PREBUILT_WIREGUARD_VERSION_HEADER="${UPSTREAM_ROOT}/client/macos/gobridge/wireguard-go-version.h"
BRIDGE_ENTITLEMENTS_PATH="${SCRIPT_DIR}/bridge/etoVPNMacBridge.entitlements"
PACKET_TUNNEL_ENTITLEMENTS_PATH="${SCRIPT_DIR}/packet-tunnel/etoVPNPacketTunnel.entitlements"
USE_NATIVE_TEAM_SIGNING="0"

if [[ -n "${DEVELOPMENT_TEAM}" ]]; then
  USE_NATIVE_TEAM_SIGNING="1"
fi

if [[ -z "${DERIVED_DATA_PATH}" ]]; then
  DERIVED_DATA_PATH="${REPO_ROOT}/${OUTPUT_ROOT}/.derived/${RUNTIME_IDENTIFIER}"
fi

rm -rf "${OUTPUT_DIR}"
mkdir -p "${OUTPUT_DIR}"

hydrate_amneziawg_apple() {
  if [[ -f "${WG_APPLE_WIREGUARD_ADAPTER}" ]]; then
    return
  fi

  if ! command -v git >/dev/null 2>&1; then
    echo "git is required only when amneziawg-apple sources are missing." >&2
    echo "Either install git or provide the repo archive with .research/amnezia-client already included." >&2
    exit 1
  fi

  if [[ -d "${UPSTREAM_ROOT}/.git" ]]; then
    git -C "${UPSTREAM_ROOT}" submodule sync -- client/3rd/amneziawg-apple >/dev/null 2>&1 || true
    git -C "${UPSTREAM_ROOT}" submodule update --init --depth 1 client/3rd/amneziawg-apple >/dev/null 2>&1 || true
  fi

  if [[ ! -f "${WG_APPLE_WIREGUARD_ADAPTER}" ]]; then
    rm -rf "${WG_APPLE_DIR}"
    git clone --depth 1 https://github.com/amnezia-vpn/amneziawg-apple "${WG_APPLE_DIR}"
  fi

  if git -C "${WG_APPLE_DIR}" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    git -C "${WG_APPLE_DIR}" fetch --depth 1 origin "${AMNEZIAWG_APPLE_REF}" >/dev/null 2>&1 || true
    git -C "${WG_APPLE_DIR}" checkout -f "${AMNEZIAWG_APPLE_REF}" >/dev/null 2>&1 || true
  fi

  if [[ ! -f "${WG_APPLE_WIREGUARD_ADAPTER}" ]]; then
    echo "Unable to hydrate amneziawg-apple sources under ${WG_APPLE_DIR}." >&2
    exit 1
  fi
}

prepare_wireguard_go_artifacts() {
  mkdir -p "${WIREGUARD_GENERATED_DIR}" "${WIREGUARD_GO_TEMP_DIR}"

  if [[ -f "${PREBUILT_WIREGUARD_LIB}" ]]; then
    cp "${PREBUILT_WIREGUARD_LIB}" "${WIREGUARD_GENERATED_DIR}/libwg-go.a"

    if [[ -f "${PREBUILT_WIREGUARD_VERSION_HEADER}" ]]; then
      cp "${PREBUILT_WIREGUARD_VERSION_HEADER}" "${WIREGUARD_GENERATED_DIR}/wireguard-go-version.h"
    else
      cat > "${WIREGUARD_GENERATED_DIR}/wireguard-go-version.h" <<'EOF'
#define WIREGUARD_GO_VERSION "0.0.0"
EOF
    fi

    return
  fi

  if ! command -v go >/dev/null 2>&1; then
    echo "go is required to build libwg-go.a when no prebuilt macOS WireGuard library is present." >&2
    exit 1
  fi

  make -C "${WIREGUARD_GO_DIR}" clean >/dev/null 2>&1 || true
  make -C "${WIREGUARD_GO_DIR}" \
    build \
    version-header \
    DESTDIR="${WIREGUARD_GENERATED_DIR}" \
    CONFIGURATION_BUILD_DIR="${WIREGUARD_GENERATED_DIR}" \
    CONFIGURATION_TEMP_DIR="${WIREGUARD_GO_TEMP_DIR}" \
    PLATFORM_NAME=macosx \
    ARCHS="x86_64 arm64"

  if [[ ! -f "${WIREGUARD_GENERATED_DIR}/libwg-go.a" ]]; then
    echo "libwg-go.a was not produced under ${WIREGUARD_GENERATED_DIR}." >&2
    exit 1
  fi

  if [[ ! -f "${WIREGUARD_GENERATED_DIR}/wireguard-go-version.h" ]]; then
    echo "wireguard-go-version.h was not produced under ${WIREGUARD_GENERATED_DIR}." >&2
    exit 1
  fi
}

hydrate_amneziawg_apple
prepare_wireguard_go_artifacts

strip_macos_detritus() {
  if ! command -v xattr >/dev/null 2>&1; then
    return
  fi

  local path
  for path in \
    "${SCRIPT_DIR}" \
    "${WG_APPLE_DIR}" \
    "${WIREGUARD_GENERATED_DIR}" \
    "${DERIVED_DATA_PATH}"
  do
    if [[ -e "${path}" ]]; then
      xattr -cr "${path}" >/dev/null 2>&1 || true
    fi
  done
}

strip_macos_detritus

manual_codesign_target() {
  local target_path="$1"
  local entitlements_path="${2:-}"

  if ! command -v codesign >/dev/null 2>&1; then
    return
  fi

  if [[ ! -e "${target_path}" ]]; then
    return
  fi

  xattr -cr "${target_path}" >/dev/null 2>&1 || true
  find "${target_path}" -name '._*' -delete >/dev/null 2>&1 || true

  local args=(
    --force
    --sign -
    --timestamp=none
    --generate-entitlement-der
  )

  if [[ -n "${entitlements_path}" && -f "${entitlements_path}" ]]; then
    args+=(--entitlements "${entitlements_path}")
  fi

  codesign "${args[@]}" "${target_path}"
}

if [[ "${SKIP_GENERATE}" != "1" ]]; then
  xcodegen generate --spec "${PROJECT_SPEC}"
fi

strip_macos_detritus

XCODEBUILD_ARGS=(
  -project "${PROJECT_PATH}"
  -scheme "etoVPNMacBridge"
  -configuration "${CONFIGURATION}"
  -destination "generic/platform=macOS"
  -derivedDataPath "${DERIVED_DATA_PATH}"
  build
)

if [[ -n "${DEVELOPMENT_TEAM}" ]]; then
  XCODEBUILD_ARGS+=("DEVELOPMENT_TEAM=${DEVELOPMENT_TEAM}")
fi

if [[ -n "${CODE_SIGN_STYLE}" ]]; then
  XCODEBUILD_ARGS+=("CODE_SIGN_STYLE=${CODE_SIGN_STYLE}")
fi

if [[ "${USE_NATIVE_TEAM_SIGNING}" == "1" ]]; then
  if [[ "${ALLOW_PROVISIONING_UPDATES}" == "1" ]]; then
    XCODEBUILD_ARGS+=(-allowProvisioningUpdates)
  fi
else
  XCODEBUILD_ARGS+=(
    "CODE_SIGNING_ALLOWED=NO"
    "CODE_SIGNING_REQUIRED=NO"
    "AD_HOC_CODE_SIGNING_ALLOWED=NO"
  )
fi

COPYFILE_DISABLE=1 COPY_EXTENDED_ATTRIBUTES_DISABLE=1 xcodebuild "${XCODEBUILD_ARGS[@]}"

PRODUCTS_DIR="${DERIVED_DATA_PATH}/Build/Products/${CONFIGURATION}"
BRIDGE_APP_PATH="${PRODUCTS_DIR}/etoVPNMacBridge.app"
PACKET_TUNNEL_PATH="${PRODUCTS_DIR}/etoVPNPacketTunnel.appex"
BRIDGE_BINARY_PATH="${BRIDGE_APP_PATH}/Contents/MacOS/etoVPNMacBridge"

if [[ ! -d "${BRIDGE_APP_PATH}" ]]; then
  echo "Bridge app bundle was not produced at ${BRIDGE_APP_PATH}" >&2
  exit 1
fi

if [[ ! -f "${BRIDGE_BINARY_PATH}" ]]; then
  echo "Bridge executable was not produced at ${BRIDGE_BINARY_PATH}" >&2
  exit 1
fi

cp -R "${BRIDGE_APP_PATH}" "${OUTPUT_DIR}/etoVPNMacBridge.app"
cp "${BRIDGE_BINARY_PATH}" "${OUTPUT_DIR}/etoVPNMacBridge"

if [[ -d "${PACKET_TUNNEL_PATH}" ]]; then
  cp -R "${PACKET_TUNNEL_PATH}" "${OUTPUT_DIR}/etoVPNPacketTunnel.appex"
elif [[ -d "${BRIDGE_APP_PATH}/Contents/PlugIns/etoVPNPacketTunnel.appex" ]]; then
  cp -R "${BRIDGE_APP_PATH}/Contents/PlugIns/etoVPNPacketTunnel.appex" "${OUTPUT_DIR}/etoVPNPacketTunnel.appex"
else
  echo "warning: etoVPNPacketTunnel.appex was not found in build products." >&2
fi

if [[ "${USE_NATIVE_TEAM_SIGNING}" != "1" ]]; then
  manual_codesign_target "${OUTPUT_DIR}/etoVPNPacketTunnel.appex" "${PACKET_TUNNEL_ENTITLEMENTS_PATH}"
  manual_codesign_target "${OUTPUT_DIR}/etoVPNMacBridge.app/Contents/PlugIns/etoVPNPacketTunnel.appex" "${PACKET_TUNNEL_ENTITLEMENTS_PATH}"

  if [[ -d "${OUTPUT_DIR}/etoVPNMacBridge.app/Contents/Frameworks" ]]; then
    while IFS= read -r framework_path; do
      manual_codesign_target "${framework_path}"
    done < <(find "${OUTPUT_DIR}/etoVPNMacBridge.app/Contents/Frameworks" -maxdepth 1 \( -name "*.framework" -o -name "*.dylib" \))
  fi

  manual_codesign_target "${OUTPUT_DIR}/etoVPNMacBridge.app" "${BRIDGE_ENTITLEMENTS_PATH}"
  manual_codesign_target "${OUTPUT_DIR}/etoVPNMacBridge"
fi

echo ""
echo "Built native macOS artifacts:"
echo "  Bridge app: ${OUTPUT_DIR}/etoVPNMacBridge.app"
echo "  Bridge binary: ${OUTPUT_DIR}/etoVPNMacBridge"
if [[ -d "${OUTPUT_DIR}/etoVPNPacketTunnel.appex" ]]; then
  echo "  Packet tunnel: ${OUTPUT_DIR}/etoVPNPacketTunnel.appex"
fi
