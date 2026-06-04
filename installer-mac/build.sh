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
#   --version 1.2.0        (default: 1.0.0)
#   --arch    arm64|x64|universal   (default: universal)
#
# Run from the repo root.

set -euo pipefail

# ── Defaults ──────────────────────────────────────────────────────────────────

BUNDLE_ID="com.prosliderlay.app"
APP_NAME="ProSlideRelay"
EXECUTABLE="ProSlideRelay"          # Must match AssemblyName in .csproj
TFM_DIR="net10.0-macos"             # Must match TargetFramework in .csproj

VERSION="1.0.0"
ARCH="universal"
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
        --arch)         ARCH="$2";         shift 2 ;;
        --team-id)      TEAM_ID="$2";      shift 2 ;;
        --apple-id)     APPLE_ID="$2";     shift 2 ;;
        --password)     APP_PASSWORD="$2"; shift 2 ;;
        --skip-signing) SKIP_SIGNING=true; shift ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

case "$ARCH" in
    arm64)     RIDS=("osx-arm64") ;;
    x64)       RIDS=("osx-x64") ;;
    universal) RIDS=("osx-arm64" "osx-x64") ;;
    *) echo "  ✗ --arch must be one of: arm64, x64, universal"; exit 1 ;;
esac

SIGN_APP="Developer ID Application: ${TEAM_ID}"
SIGN_PKG="Developer ID Installer: ${TEAM_ID}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PROJECT="${REPO_ROOT}/src/ProSlideRelay.Mac/ProSlideRelay.Mac.csproj"
BIN_BASE="${REPO_ROOT}/src/ProSlideRelay.Mac/bin/Release/${TFM_DIR}"
OUT_DIR="${REPO_ROOT}/publish/mac"
APP_BUNDLE="${OUT_DIR}/${APP_NAME}.app"
OUTPUT_DIR="${SCRIPT_DIR}/output"

# ── Preflight checks ──────────────────────────────────────────────────────────

echo ""
echo "  ProSlideRelay macOS build — v${VERSION} (${ARCH})"
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

# ── Step 1: Publish each architecture ─────────────────────────────────────────
#
# The .NET macOS workload produces a complete .app bundle per architecture under
# the project's bin/ directory. We disable its built-in .pkg generation
# (CreatePackage=false) and assemble / sign / package the bundle ourselves.

for rid in "${RIDS[@]}"; do
    echo "  ► Publishing ${rid}…"
    rm -rf "${BIN_BASE}/${rid}/${APP_NAME}.app"
    dotnet publish "$PROJECT" -r "$rid" -c Release \
        -p:Version="$VERSION" \
        -p:CreatePackage=false \
        --self-contained true \
        --nologo -v quiet
done

# ── Step 2: Assemble the .app bundle (universal via lipo when needed) ──────────

echo "  ► Assembling .app bundle…"

PRIMARY_RID="${RIDS[0]}"
PRIMARY_APP="${BIN_BASE}/${PRIMARY_RID}/${APP_NAME}.app"

[[ -d "$PRIMARY_APP" ]] || { echo "  ✗ build output not found: ${PRIMARY_APP}"; exit 1; }

rm -rf "$APP_BUNDLE"
mkdir -p "$OUT_DIR"
cp -R "$PRIMARY_APP" "$APP_BUNDLE"

# For a universal build, fatten the native Mach-O files (the app host executable
# and the runtime dylibs) by merging the second architecture into the copy.
if [[ "${#RIDS[@]}" -gt 1 ]]; then
    echo "  ► Creating universal binaries…"
    SECOND_RID="${RIDS[1]}"
    SECOND_APP="${BIN_BASE}/${SECOND_RID}/${APP_NAME}.app"
    [[ -d "$SECOND_APP" ]] || { echo "  ✗ build output not found: ${SECOND_APP}"; exit 1; }

    # The main executable
    lipo -create \
        "${PRIMARY_APP}/Contents/MacOS/${EXECUTABLE}" \
        "${SECOND_APP}/Contents/MacOS/${EXECUTABLE}" \
        -output "${APP_BUNDLE}/Contents/MacOS/${EXECUTABLE}"

    # Native runtime dylibs (managed .dll assemblies are architecture-neutral)
    find "${PRIMARY_APP}" -name "*.dylib" | while read -r primary_lib; do
        rel="${primary_lib#${PRIMARY_APP}/}"
        second_lib="${SECOND_APP}/${rel}"
        if [[ -f "$second_lib" ]]; then
            lipo -create "$primary_lib" "$second_lib" -output "${APP_BUNDLE}/${rel}"
        fi
    done
fi

# Write Info.plist, substituting the version placeholder. This is the bundle's
# canonical Info.plist (menu-bar-only via LSUIElement, min OS version, etc.).
sed "s/\$(BUNDLE_VERSION)/${VERSION}/g" \
    "${SCRIPT_DIR}/Info.plist" > "${APP_BUNDLE}/Contents/Info.plist"

echo "APPL????" > "${APP_BUNDLE}/Contents/PkgInfo"

# ── Step 3: Sign ──────────────────────────────────────────────────────────────
#
# lipo and the Info.plist rewrite invalidate the signature the SDK applied, so
# we always re-sign. Unsigned builds get an ad-hoc signature (sufficient to run
# locally); release builds get a Developer ID signature with the hardened runtime.

if [[ "$SKIP_SIGNING" == true ]]; then
    echo "  ► Ad-hoc signing (unsigned build)…"
    # Sign nested Mach-O files first, then the bundle — order matters.
    find "${APP_BUNDLE}" -type f \( -name "*.dylib" -o -name "createdump" \) | while read -r lib; do
        codesign --force --sign - "$lib"
    done
    codesign --force --sign - "$APP_BUNDLE"
else
    echo "  ► Signing .app bundle…"
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
fi

# ── Step 4: Notarize (signed builds only) ─────────────────────────────────────

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
fi

# ── Step 5: Build the .pkg installer ──────────────────────────────────────────

echo "  ► Building .pkg installer…"
mkdir -p "$OUTPUT_DIR"

PKG_ROOT="${OUT_DIR}/pkg-root"
rm -rf "$PKG_ROOT"
mkdir -p "${PKG_ROOT}/Applications"
cp -R "$APP_BUNDLE" "${PKG_ROOT}/Applications/"

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
