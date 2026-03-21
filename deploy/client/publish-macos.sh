#!/usr/bin/env bash
set -euo pipefail

export COPYFILE_DISABLE=1
export COPY_EXTENDED_ATTRIBUTES_DISABLE=1

CONFIGURATION="${CONFIGURATION:-Release}"
RUNTIME_IDENTIFIER="${RUNTIME_IDENTIFIER:-osx-arm64}"
VERSION="${VERSION:-0.1.9}"
OUTPUT_ROOT="${OUTPUT_ROOT:-artifacts/client-publish}"
ZIP_PACKAGE="${ZIP_PACKAGE:-0}"
SKIP_NATIVE_BUILD="${SKIP_NATIVE_BUILD:-0}"
NATIVE_OUTPUT_DIR="${NATIVE_OUTPUT_DIR:-}"
BUNDLE_IDENTIFIER="${BUNDLE_IDENTIFIER:-com.etovpn.desktop}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
BRIDGE_ENTITLEMENTS="${REPO_ROOT}/native/macos/bridge/etoVPNMacBridge.entitlements"
PACKET_TUNNEL_ENTITLEMENTS="${REPO_ROOT}/native/macos/packet-tunnel/etoVPNPacketTunnel.entitlements"
PROJECT_PATH="${REPO_ROOT}/UI/VpnClient.UI.csproj"
PUBLISH_DIR="${REPO_ROOT}/${OUTPUT_ROOT}/${RUNTIME_IDENTIFIER}"
STAGING_DIR="${PUBLISH_DIR}/.publish"
APP_BUNDLE_DIR="${PUBLISH_DIR}/etoVPN.app"
APP_CONTENTS_DIR="${APP_BUNDLE_DIR}/Contents"
APP_MACOS_DIR="${APP_CONTENTS_DIR}/MacOS"
APP_RESOURCES_DIR="${APP_CONTENTS_DIR}/Resources"
APP_HELPERS_DIR="${APP_CONTENTS_DIR}/Helpers"
APP_PLUGINS_DIR="${APP_CONTENTS_DIR}/PlugIns"
PUBLISH_PROFILE="${RUNTIME_IDENTIFIER}-selfcontained"
INFO_PLIST_TEMPLATE="${REPO_ROOT}/deploy/client/macos/Info.plist"
ICON_SOURCE="${REPO_ROOT}/UI/Assets/shield.png"
NATIVE_BUILD_SCRIPT="${REPO_ROOT}/native/macos/build-native.sh"

if command -v dotnet >/dev/null 2>&1; then
  DOTNET_BIN="dotnet"
elif command -v dotnet.exe >/dev/null 2>&1; then
  DOTNET_BIN="dotnet.exe"
else
  echo "dotnet is required but was not found in PATH." >&2
  exit 1
fi

DOTNET_PROJECT_PATH="${PROJECT_PATH}"
DOTNET_PUBLISH_DIR="${STAGING_DIR}"

if [[ "${DOTNET_BIN}" == "dotnet.exe" ]] && command -v cygpath >/dev/null 2>&1; then
  DOTNET_PROJECT_PATH="$(cygpath -w "${PROJECT_PATH}")"
  DOTNET_PUBLISH_DIR="$(cygpath -w "${STAGING_DIR}")"
fi

strip_macos_detritus() {
  local path="$1"
  local parent_dir=""

  if [[ ! -e "${path}" ]]; then
    return
  fi

  if [[ -f "${path}" ]]; then
    parent_dir="$(dirname "${path}")"
  fi

  if command -v xattr >/dev/null 2>&1; then
    xattr -cr "${path}" >/dev/null 2>&1 || true
    if [[ -n "${parent_dir}" ]]; then
      xattr -cr "${parent_dir}" >/dev/null 2>&1 || true
    fi
  fi

  if command -v dot_clean >/dev/null 2>&1; then
    dot_clean -m "${path}" >/dev/null 2>&1 || true
    if [[ -n "${parent_dir}" ]]; then
      dot_clean -m "${parent_dir}" >/dev/null 2>&1 || true
    fi
  fi

  find "${path}" -name '._*' -delete >/dev/null 2>&1 || true
  find "${path}" -name '.DS_Store' -delete >/dev/null 2>&1 || true
  find "${path}" -name $'Icon\r' -delete >/dev/null 2>&1 || true

  if [[ -n "${parent_dir}" ]]; then
    rm -f "${parent_dir}/._$(basename "${path}")" >/dev/null 2>&1 || true
  fi
}

rewrite_clean_file() {
  local source_path="$1"
  local destination_path="$2"
  local permissions=""

  rm -f "${destination_path}"
  mkdir -p "$(dirname "${destination_path}")"

  cat "${source_path}" > "${destination_path}"

  if command -v stat >/dev/null 2>&1; then
    permissions="$(stat -f %Lp "${source_path}" 2>/dev/null || true)"
  fi

  if [[ -n "${permissions}" ]]; then
    chmod "${permissions}" "${destination_path}" >/dev/null 2>&1 || true
  elif [[ -x "${source_path}" ]]; then
    chmod +x "${destination_path}" >/dev/null 2>&1 || true
  fi

  strip_macos_detritus "${destination_path}"
}

copy_clean() {
  local source_path="$1"
  local destination_path="$2"

  rm -rf "${destination_path}"

  if [[ -f "${source_path}" ]]; then
    rewrite_clean_file "${source_path}" "${destination_path}"
  elif command -v ditto >/dev/null 2>&1; then
    ditto --noextattr --noqtn --norsrc "${source_path}" "${destination_path}"
  else
    cp -R "${source_path}" "${destination_path}"
  fi

  strip_macos_detritus "${destination_path}"
}

normalize_app_bundle() {
  local normalized_bundle="${PUBLISH_DIR}/.etoVPN.normalized.app"

  rm -rf "${normalized_bundle}"

  if command -v ditto >/dev/null 2>&1; then
    ditto --noextattr --noqtn --norsrc "${APP_BUNDLE_DIR}" "${normalized_bundle}"
    rm -rf "${APP_BUNDLE_DIR}"
    mv "${normalized_bundle}" "${APP_BUNDLE_DIR}"
  fi

  strip_macos_detritus "${APP_BUNDLE_DIR}"
}

sign_app_macos_binaries() {
  if [[ ! -d "${APP_MACOS_DIR}" ]]; then
    return
  fi

  while IFS= read -r binary_path; do
    manual_codesign_target "${binary_path}"
  done < <(
    find "${APP_MACOS_DIR}" -maxdepth 1 -type f \
      \( -name "*.dll" -o -name "*.dylib" -o -name "*.so" -o -name "VpnClient.UI" -o -name "createdump" \) \
      | sort
  )
}

strip_path_node_metadata() {
  local path="$1"

  if [[ ! -e "${path}" ]]; then
    return
  fi

  if command -v xattr >/dev/null 2>&1; then
    xattr -c "${path}" >/dev/null 2>&1 || true
  fi

  rm -f "${path}/.DS_Store" >/dev/null 2>&1 || true
  find "${path}" -maxdepth 1 -name '._*' -delete >/dev/null 2>&1 || true
}

manual_codesign_target() {
  local target_path="$1"
  local entitlements_path="${2:-}"

  if ! command -v codesign >/dev/null 2>&1; then
    return
  fi

  if [[ ! -e "${target_path}" ]]; then
    return
  fi

  strip_macos_detritus "${target_path}"

  local args=(
    --force
    --sign -
    --timestamp=none
    --generate-entitlement-der
  )

  if [[ -n "${entitlements_path}" && -f "${entitlements_path}" ]]; then
    args+=(--entitlements "${entitlements_path}")
  fi

  if codesign "${args[@]}" "${target_path}"; then
    return
  fi

  local normalized_target="${target_path}.normalized"

  if [[ -f "${target_path}" ]]; then
    rewrite_clean_file "${target_path}" "${normalized_target}"
  elif command -v ditto >/dev/null 2>&1; then
    rm -rf "${normalized_target}"
    ditto --noextattr --noqtn --norsrc "${target_path}" "${normalized_target}"
  else
    rm -rf "${normalized_target}"
    cp -R "${target_path}" "${normalized_target}"
  fi

  if [[ -e "${normalized_target}" ]]; then
    rm -rf "${target_path}"
    mv "${normalized_target}" "${target_path}"
  fi

  strip_macos_detritus "${target_path}"
  codesign "${args[@]}" "${target_path}"
}

if [[ -d "${PUBLISH_DIR}" ]]; then
  rm -rf "${PUBLISH_DIR}"
fi

mkdir -p "${STAGING_DIR}"

"${DOTNET_BIN}" publish "${DOTNET_PROJECT_PATH}" \
  -c "${CONFIGURATION}" \
  -r "${RUNTIME_IDENTIFIER}" \
  /p:PublishProfile="${PUBLISH_PROFILE}" \
  /p:Version="${VERSION}" \
  -o "${DOTNET_PUBLISH_DIR}"

if [[ "${SKIP_NATIVE_BUILD}" != "1" ]]; then
  if [[ ! -x "${NATIVE_BUILD_SCRIPT}" ]]; then
    echo "native build script is missing or not executable: ${NATIVE_BUILD_SCRIPT}" >&2
    echo "Set SKIP_NATIVE_BUILD=1 to package only the Avalonia desktop publish output." >&2
    exit 1
  fi

  "${NATIVE_BUILD_SCRIPT}" \
    --configuration "${CONFIGURATION}" \
    --runtime "${RUNTIME_IDENTIFIER}"
fi

if [[ -z "${NATIVE_OUTPUT_DIR}" ]]; then
  NATIVE_OUTPUT_DIR="${REPO_ROOT}/artifacts/macos-native/${RUNTIME_IDENTIFIER}"
fi

mkdir -p "${APP_MACOS_DIR}" "${APP_RESOURCES_DIR}" "${APP_HELPERS_DIR}" "${APP_PLUGINS_DIR}"

find "${STAGING_DIR}" -mindepth 1 -maxdepth 1 -print0 | while IFS= read -r -d '' entry; do
  copy_clean "${entry}" "${APP_MACOS_DIR}/$(basename "${entry}")"
done

sed \
  -e "s|__BUNDLE_IDENTIFIER__|${BUNDLE_IDENTIFIER}|g" \
  -e "s|__SHORT_VERSION__|${VERSION}|g" \
  -e "s|__BUNDLE_VERSION__|${VERSION}|g" \
  "${INFO_PLIST_TEMPLATE}" > "${APP_CONTENTS_DIR}/Info.plist"

if [[ -f "${ICON_SOURCE}" ]]; then
  copy_clean "${ICON_SOURCE}" "${APP_RESOURCES_DIR}/shield.png"
fi

strip_macos_detritus "${APP_BUNDLE_DIR}"

if command -v iconutil >/dev/null 2>&1 && command -v sips >/dev/null 2>&1 && [[ -f "${ICON_SOURCE}" ]]; then
  ICONSET_DIR="${PUBLISH_DIR}/.iconset"
  mkdir -p "${ICONSET_DIR}"

  sips -z 16 16 "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_16x16.png" >/dev/null
  sips -z 32 32 "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_16x16@2x.png" >/dev/null
  sips -z 32 32 "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_32x32.png" >/dev/null
  sips -z 64 64 "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_32x32@2x.png" >/dev/null
  sips -z 128 128 "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_128x128.png" >/dev/null
  sips -z 256 256 "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_128x128@2x.png" >/dev/null
  sips -z 256 256 "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_256x256.png" >/dev/null
  sips -z 512 512 "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_256x256@2x.png" >/dev/null
  sips -z 512 512 "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_512x512.png" >/dev/null
  sips -z 1024 1024 "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_512x512@2x.png" >/dev/null
  if ! iconutil -c icns "${ICONSET_DIR}" -o "${APP_RESOURCES_DIR}/etoVPN.icns"; then
    echo "warning: iconutil failed to build etoVPN.icns; continuing without .icns bundle icon" >&2
    rm -f "${APP_RESOURCES_DIR}/etoVPN.icns"
  fi
  rm -rf "${ICONSET_DIR}"
fi

if [[ -f "${NATIVE_OUTPUT_DIR}/etoVPNMacBridge" ]]; then
  copy_clean "${NATIVE_OUTPUT_DIR}/etoVPNMacBridge" "${APP_HELPERS_DIR}/etoVPNMacBridge"
  chmod +x "${APP_HELPERS_DIR}/etoVPNMacBridge"
else
  echo "warning: native helper was not found at ${NATIVE_OUTPUT_DIR}/etoVPNMacBridge" >&2
fi

if [[ -d "${NATIVE_OUTPUT_DIR}/etoVPNPacketTunnel.appex" ]]; then
  copy_clean "${NATIVE_OUTPUT_DIR}/etoVPNPacketTunnel.appex" "${APP_PLUGINS_DIR}/etoVPNPacketTunnel.appex"
else
  echo "warning: packet tunnel extension was not found at ${NATIVE_OUTPUT_DIR}/etoVPNPacketTunnel.appex" >&2
fi

strip_macos_detritus "${APP_BUNDLE_DIR}"

if [[ -d "${APP_CONTENTS_DIR}/Frameworks" ]]; then
  while IFS= read -r framework_path; do
    manual_codesign_target "${framework_path}"
  done < <(find "${APP_CONTENTS_DIR}/Frameworks" -maxdepth 1 \( -name "*.framework" -o -name "*.dylib" \))
fi

normalize_app_bundle
sign_app_macos_binaries
manual_codesign_target "${APP_HELPERS_DIR}/etoVPNMacBridge"
manual_codesign_target "${APP_PLUGINS_DIR}/etoVPNPacketTunnel.appex" "${PACKET_TUNNEL_ENTITLEMENTS}"
strip_path_node_metadata "${APP_BUNDLE_DIR}"
manual_codesign_target "${APP_BUNDLE_DIR}" "${BRIDGE_ENTITLEMENTS}"

rm -rf "${STAGING_DIR}"

echo ""
echo "Published macOS desktop app bundle: ${APP_BUNDLE_DIR}"
echo "Native artifact source: ${NATIVE_OUTPUT_DIR}"

if [[ "${ZIP_PACKAGE}" == "1" ]]; then
  ZIP_PATH="${REPO_ROOT}/${OUTPUT_ROOT}/etoVPN-${RUNTIME_IDENTIFIER}.zip"
  rm -f "${ZIP_PATH}"
  ditto -c -k --sequesterRsrc --keepParent "${APP_BUNDLE_DIR}" "${ZIP_PATH}"
  echo "Created zip package: ${ZIP_PATH}"
fi
