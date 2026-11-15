#!/usr/bin/env bash
#
# Builds Parlotype.app for macOS (ADR-077): publishes the desktop app
# self-contained for one architecture, wraps it in an application bundle and
# signs it.
#
#   scripts/build-macos-app.sh                      # this Mac's architecture
#   scripts/build-macos-app.sh --arch arm64         # Apple Silicon, from any Mac
#   scripts/build-macos-app.sh --version 1.2.0 --sign "Developer ID Application: ..."
#
# The bundle lands in artifacts/macos/<arch>/Parlotype.app. A bundle is what
# gives the app its own identity: macOS files the Accessibility and Microphone
# grants under the bundle identifier, and a bare executable started from a
# terminal borrows the terminal's instead.
#
# Signing defaults to ad hoc ("-"), which is enough to run on the machine that
# built it. Anything meant for another Mac needs a Developer ID identity and
# notarization, which this script does not do.
set -euo pipefail

arch=""
configuration="Release"
version="0.0.0-dev"
identity="-"

while [ $# -gt 0 ]; do
  case "$1" in
    --arch) arch="$2"; shift 2 ;;
    --configuration) configuration="$2"; shift 2 ;;
    --version) version="$2"; shift 2 ;;
    --sign) identity="$2"; shift 2 ;;
    -h|--help) sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

if [ "$(uname -s)" != "Darwin" ]; then
  echo "This script builds a macOS bundle and has to run on macOS." >&2
  exit 1
fi

if [ -z "$arch" ]; then
  case "$(uname -m)" in
    arm64) arch="arm64" ;;
    *) arch="x64" ;;
  esac
fi

case "$arch" in
  # Intel: verified on macOS 13. Older systems get Whisper instead of Parakeet
  # and the fallback ONNX Runtime for voice activity detection.
  x64) min_macos="13.0" ;;
  # Apple Silicon ships no fallback, and every such Mac can run macOS 15, the
  # oldest release the bundled speech libraries declare support for.
  arm64) min_macos="15.0" ;;
  *) echo "Unsupported architecture '$arch' (use x64 or arm64)." >&2; exit 2 ;;
esac

repo="$(cd "$(dirname "$0")/.." && pwd)"
rid="osx-$arch"
out="$repo/artifacts/macos/$arch"
publish="$out/publish"
app="$out/Parlotype.app"

echo "==> Publishing $rid ($configuration, version $version)"
rm -rf "$publish" "$app"
dotnet publish "$repo/src/Parlotype.Desktop" \
  -c "$configuration" -r "$rid" --self-contained true \
  -p:Version="$version" \
  -o "$publish" --nologo -v quiet

echo "==> Assembling $app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -R "$publish/." "$app/Contents/MacOS/"

# CFBundleVersion takes digits and dots only; a SemVer suffix stays in the
# assembly's informational version.
bundle_version="${version%%[-+]*}"
sed -e "s/__VERSION__/$bundle_version/g" -e "s/__MIN_MACOS__/$min_macos/g" \
  "$repo/packaging/macos/Info.plist" > "$app/Contents/Info.plist"

echo "==> Building the icon"
# The only artwork in the repository is the Windows .ico (256 px at most), so
# the larger sizes here are upscaled. Replace with real 1024 px art when there is some.
iconset="$out/Parlotype.iconset"
rm -rf "$iconset"
mkdir -p "$iconset"
sips -s format png "$repo/src/Parlotype.Desktop/Assets/parlotype.ico" --out "$out/icon-source.png" >/dev/null
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$out/icon-source.png" --out "$iconset/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  sips -z "$double" "$double" "$out/icon-source.png" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$iconset" -o "$app/Contents/Resources/Parlotype.icns"
rm -rf "$iconset" "$out/icon-source.png"

echo "==> Signing ($([ "$identity" = "-" ] && echo "ad hoc" || echo "$identity"))"
# A .NET app keeps its assemblies and data files next to the executable, in
# Contents/MacOS, where codesign treats every file as code: each one needs a
# signature of its own (kept in an extended attribute for a file that is not
# Mach-O) before the bundle can be sealed. So: every file first, then the
# bundle, whose signature is also the main executable's.
sign() {
  if [ "$identity" = "-" ]; then
    codesign --force --sign - "$1"
  else
    codesign --force --timestamp --options runtime \
      --entitlements "$repo/packaging/macos/Parlotype.entitlements" --sign "$identity" "$1"
  fi
}

main_executable="$app/Contents/MacOS/Parlotype"
find "$app/Contents/MacOS" -type f ! -path "$main_executable" -print0 |
  while IFS= read -r -d '' file; do
    sign "$file" 2>/dev/null || { echo "Could not sign $file" >&2; exit 1; }
  done

sign "$app"

codesign --verify --strict "$app"

size="$(du -sh "$app" | cut -f1)"
echo "==> Done: $app ($size, $rid, macOS $min_macos+)"
echo "    Run it with: open \"$app\""
