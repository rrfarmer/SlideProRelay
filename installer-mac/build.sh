#!/usr/bin/env bash
# ProSlideRelay macOS build script
#
# UNSIGNED (anyone — for local development/testing):
#   ./installer-mac/build.sh --skip-signing
#
# SIGNED + NOTARIZED (maintainer release build):
#   ./installer-mac/build.sh \
#     --team-id   MV2GMSQED3 \
#     --apple-id  you@example.com \
#     --password  <app-specific-password from appleid.apple.com>
#
# Optional in both modes:
#   --version 1.2.0   (default: 1.0.0)
#
# Run from the repo root.

set -euo pipefail

# ── Defaults ──────────────────────────────────────────────────────────────────

BUNDLE_ID="com.prosliderlay.app"
APP_NAME="ProSlideRelay"
EXECUTABLE="ProSlideRelay"          # Must match AssemblyName in .csproj

VERSION="1.0.0"
TEAM_ID=""
APPLE_ID=""
APP_PASSWORD=""
SKIP_SIGNING=false

# Source local signing config if present (gitignored — never committed)
_SCRIPT_DIR_EARLY="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ -f "${_SCRIPT_DIR_EARLY}/signing-config.sh" ]]; then
    # shellcheck source=signing-config.sh.example
    source "${_SCRIPT_DIR_EARLY}/signing-config.sh"
fi

# ── Argument parsing ──────────────────────────────────────────────────────────

while [[ $# -gt 0 ]]; do
    case "$1" in
        --version)      VERSION="$2";      shift 2 ;;
        --team-id)      TEAM_ID="$2";      shift 2 ;;
        --apple-id)     APPLE_ID="$2";     shift 2 ;;
        --password)     APP_PASSWORD="$2"; shift 2 ;;
        --skip-signing) SKIP_SIGNING=true; shift ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

SIGN_APP="Developer ID Application: ${TEAM_ID}"
SIGN_PKG="Developer ID Installer: ${TEAM_ID}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PROJECT="${REPO_ROOT}/src/ProSlideRelay.Mac/ProSlideRelay.Mac.csproj"
OUT_DIR="${REPO_ROOT}/publish/mac"
APP_BUNDLE="${OUT_DIR}/${APP_NAME}.app"
OUTPUT_DIR="${SCRIPT_DIR}/output"

# ── Preflight checks ──────────────────────────────────────────────────────────

echo ""
echo "  ProSlideRelay macOS build — v${VERSION}"
if [[ "$SKIP_SIGNING" == true ]]; then
    echo "  Mode: UNSIGNED (local use only)"
else
    echo "  Mode: SIGNED + NOTARIZED"
fi
echo ""

command -v dotnet   >/dev/null || { echo "  ✗ dotnet not found"; exit 1; }
command -v lipo     >/dev/null || { echo "  ✗ lipo not found — run: xcode-select --install"; exit 1; }
command -v codesign >/dev/null || { echo "  ✗ codesign not found — run: xcode-select --install"; exit 1; }

if [[ "$SKIP_SIGNING" == false ]]; then
    [[ -z "$TEAM_ID"       ]] && { echo "  ✗ --team-id is required for signed builds";  exit 1; }
    [[ -z "$APPLE_ID"      ]] && { echo "  ✗ --apple-id is required for signed builds"; exit 1; }
    [[ -z "$APP_PASSWORD"  ]] && { echo "  ✗ --password is required for signed builds"; exit 1; }
fi

# ── Step 1: Publish for both architectures ────────────────────────────────────

echo "  ► Publishing arm64…"
dotnet publish "$PROJECT" -r osx-arm64 -c Release \
    -p:Version="$VERSION" \
    --self-contained true \
    -o "${OUT_DIR}/arm64" \
    --nologo -v quiet

echo "  ► Publishing x64…"
dotnet publish "$PROJECT" -r osx-x64 -c Release \
    -p:Version="$VERSION" \
    --self-contained true \
    -o "${OUT_DIR}/x64" \
    --nologo -v quiet

# ── Step 2: Universal binary ──────────────────────────────────────────────────

echo "  ► Creating universal binary…"

ARM64_BIN="${OUT_DIR}/arm64/${EXECUTABLE}"
X64_BIN="${OUT_DIR}/x64/${EXECUTABLE}"
UNIVERSAL_BIN="${OUT_DIR}/${EXECUTABLE}"

[[ -f "$ARM64_BIN" ]] || { echo "  ✗ arm64 binary not found: ${ARM64_BIN}"; exit 1; }
[[ -f "$X64_BIN"   ]] || { echo "  ✗ x64 binary not found: ${X64_BIN}";    exit 1; }

lipo -create "$ARM64_BIN" "$X64_BIN" -output "$UNIVERSAL_BIN"

# ── Step 3: Assemble the .app bundle ─────────────────────────────────────────

echo "  ► Assembling .app bundle…"

MACOS_DIR="${APP_BUNDLE}/Contents/MacOS"
RESOURCES_DIR="${APP_BUNDLE}/Contents/Resources"

rm -rf "$APP_BUNDLE"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"

# Managed assemblies are arch-neutral — copy from arm64 publish
rsync -a --exclude="${EXECUTABLE}" "${OUT_DIR}/arm64/" "$MACOS_DIR/"

# Replace the arch-specific executable with the universal one
cp "$UNIVERSAL_BIN" "${MACOS_DIR}/${EXECUTABLE}"
chmod +x "${MACOS_DIR}/${EXECUTABLE}"

# Lipo any native dylibs that differ between architectures
find "${OUT_DIR}/arm64" -name "*.dylib" | while read -r arm64_lib; do
    rel="${arm64_lib#${OUT_DIR}/arm64/}"
    x64_lib="${OUT_DIR}/x64/${rel}"
    if [[ -f "$x64_lib" ]]; then
        lipo -create "$arm64_lib" "$x64_lib" \
            -output "${MACOS_DIR}/${rel}" 2>/dev/null || true
    fi
done

# Write Info.plist, substituting the version placeholder
sed "s/\$(BUNDLE_VERSION)/${VERSION}/g" \
    "${SCRIPT_DIR}/Info.plist" > "${APP_BUNDLE}/Contents/Info.plist"

echo "APPL????" > "${APP_BUNDLE}/Contents/PkgInfo"

# ── Step 4: Sign (signed builds only) ────────────────────────────────────────

if [[ "$SKIP_SIGNING" == false ]]; then
    echo "  ► Signing .app bundle…"

    # Sign nested dylibs first — order matters
    find "${APP_BUNDLE}" -type f \( -name "*.dylib" -o -name "createdump" \) | while read -r lib; do
        codesign --force --sign "$SIGN_APP" \
            --options runtime \
            --entitlements "${SCRIPT_DIR}/Entitlements.plist" \
            "$lib"
    done

    codesign --force --sign "$SIGN_APP" \
        --options runtime \
        --entitlements "${SCRIPT_DIR}/Entitlements.plist" \
        --timestamp \
        "$APP_BUNDLE"

    echo "  ► Verifying signature…"
    codesign --verify --deep --strict --verbose=2 "$APP_BUNDLE"
else
    echo "  ► Skipping code signing (--skip-signing)"
fi

# ── Step 5: Notarize (signed builds only) ────────────────────────────────────

if [[ "$SKIP_SIGNING" == false ]]; then
    echo "  ► Zipping for notarization…"
    ZIP_PATH="${OUT_DIR}/${APP_NAME}-${VERSION}.zip"
    ditto -c -k --keepParent "$APP_BUNDLE" "$ZIP_PATH"

    echo "  ► Submitting to Apple notarization (~1 min)…"
    xcrun notarytool submit "$ZIP_PATH" \
        --apple-id "$APPLE_ID" \
        --team-id  "$TEAM_ID" \
        --password "$APP_PASSWORD" \
        --wait

    echo "  ► Stapling notarization ticket…"
    xcrun stapler staple "$APP_BUNDLE"
else
    echo "  ► Skipping notarization (--skip-signing)"
fi

# ── Step 6: Build the .pkg installer ─────────────────────────────────────────

echo "  ► Building .pkg installer…"
mkdir -p "$OUTPUT_DIR"

PKG_ROOT="${OUT_DIR}/pkg-root"
rm -rf "$PKG_ROOT"
mkdir -p "${PKG_ROOT}/Applications"
cp -r "$APP_BUNDLE" "${PKG_ROOT}/Applications/"

COMPONENT_PKG="${OUTPUT_DIR}/ProSlideRelay-component.pkg"
FINAL_PKG="${OUTPUT_DIR}/${APP_NAME}-${VERSION}.pkg"

pkgbuild \
    --root              "$PKG_ROOT" \
    --identifier        "$BUNDLE_ID" \
    --version           "$VERSION" \
    --install-location  "/" \
    "$COMPONENT_PKG"

if [[ "$SKIP_SIGNING" == false ]]; then
    productbuild \
        --distribution  "${SCRIPT_DIR}/distribution.xml" \
        --package-path  "$OUTPUT_DIR" \
        --resources     "${SCRIPT_DIR}" \
        --sign          "$SIGN_PKG" \
        --timestamp \
        "$FINAL_PKG"
else
    productbuild \
        --distribution  "${SCRIPT_DIR}/distribution.xml" \
        --package-path  "$OUTPUT_DIR" \
        --resources     "${SCRIPT_DIR}" \
        "$FINAL_PKG"
fi

rm -f "$COMPONENT_PKG"

# ── Done ──────────────────────────────────────────────────────────────────────

echo ""
if [[ "$SKIP_SIGNING" == true ]]; then
    echo "  ✓ Unsigned installer ready: ${FINAL_PKG}"
    echo ""
    echo "  Note: Because this build is unsigned, macOS Gatekeeper will"
    echo "  warn when opening it on other Macs. On your own Mac, right-click"
    echo "  the .pkg and choose Open to bypass the warning."
else
    echo "  ✓ Signed installer ready: ${FINAL_PKG}"
fi
echo ""
