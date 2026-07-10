#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
MAC_DIR="$ROOT_DIR/src-mac"
DIST_DIR="$ROOT_DIR/dist/K3_USB_An_Toan_K3_Portable"
USB_TARGET=""

usage() {
  cat <<'USAGE'
Usage:
  scripts/build-release-usb-mac.sh [--usb /Volumes/USB-DATA] [--dist path]

Builds the macOS app and prepares the portable USB release layout.
When --usb is provided, app/tools are copied to that USB without deleting vault data.
USAGE
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --usb)
      USB_TARGET="${2:-}"
      shift 2
      ;;
    --dist)
      DIST_DIR="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [ -n "$USB_TARGET" ] && [ ! -d "$USB_TARGET" ]; then
  echo "USB target does not exist: $USB_TARGET" >&2
  exit 1
fi

echo "==> Building macOS release"
(cd "$MAC_DIR" && swift build -c release)

BIN="$MAC_DIR/.build/release/K3UsbSafeMac"
BUNDLE="$MAC_DIR/.build/release/K3UsbSafeMac_K3UsbSafeMac.bundle"
if [ ! -x "$BIN" ]; then
  echo "Missing build output: $BIN" >&2
  exit 1
fi

prepare_layout() {
  local target="$1"
  local app="$target/K3 Mac.app"

  mkdir -p "$target/mac"
  mkdir -p "$target/tools/rules"
  mkdir -p "$app/Contents/MacOS"
  mkdir -p "$app/Contents/Resources"

  cp "$BIN" "$target/mac/K3UsbSafeMac"
  cp "$BIN" "$app/Contents/MacOS/K3UsbSafeMac"
  chmod +x "$target/mac/K3UsbSafeMac" "$app/Contents/MacOS/K3UsbSafeMac"

  if [ -d "$BUNDLE" ]; then
    rm -rf "$app/Contents/Resources/K3UsbSafeMac_K3UsbSafeMac.bundle"
    cp -R "$BUNDLE" "$app/Contents/Resources/"
  fi

  cat > "$app/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDisplayName</key>
  <string>K3 Mac</string>
  <key>CFBundleExecutable</key>
  <string>K3UsbSafeMac</string>
  <key>CFBundleIdentifier</key>
  <string>vn.k3.usbsafe.mac</string>
  <key>CFBundleName</key>
  <string>K3 Mac</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>LSMinimumSystemVersion</key>
  <string>13.0</string>
</dict>
</plist>
PLIST

  cat > "$target/Chay_Mac.command" <<'COMMAND'
#!/bin/sh
DIR="$(cd "$(dirname "$0")" && pwd)"
open "$DIR/K3 Mac.app"
COMMAND
  chmod +x "$target/Chay_Mac.command"

  if [ -f "$ROOT_DIR/tools/rules/k3-rules.json" ]; then
    cp "$ROOT_DIR/tools/rules/k3-rules.json" "$target/tools/rules/k3-rules.json"
  fi

  if [ -x "$ROOT_DIR/tools/mac-arm64/clamav/clamscan" ] || [ -x "$ROOT_DIR/tools/mac-arm64/clamav/bin/clamscan" ]; then
    mkdir -p "$target/tools"
    rm -rf "$target/tools/mac-arm64"
    cp -R "$ROOT_DIR/tools/mac-arm64" "$target/tools/"
  elif [ ! -d "$target/tools/mac-arm64" ]; then
    echo "Note: mac-arm64 ClamAV bundle not found in repo; leaving it absent in $target"
  else
    echo "Note: mac-arm64 ClamAV bundle not found in repo; keeping existing $target/tools/mac-arm64"
  fi

  if [ -f "$ROOT_DIR/favicon.ico" ]; then
    cp "$ROOT_DIR/favicon.ico" "$target/favicon.ico"
  fi

  cat > "$target/README-MAC-K3.txt" <<'README'
USB An Toan K3 - macOS

1. Mo USB trong Finder.
2. Double click "K3 Mac.app" hoac "Chay_Mac.command".
3. Neu macOS can xac nhan bao mat, vao System Settings > Privacy & Security va Allow/Open Anyway.

Du lieu ket nam trong .vault, .vault_decoy va .vault_config.json tren USB.
Khi update app, khong xoa cac file/thumuc an nay.
README

  mkdir -p "$target/.vault" "$target/.vault_decoy" "$target/BaoMat"
  write_integrity_manifest "$target"
  write_release_report "$target"
  chflags hidden "$target/.k3_integrity_manifest.json" 2>/dev/null || true
}

write_integrity_manifest() {
  local target="$1"
  local manifest="$target/.k3_integrity_manifest.json"
  local first=1
  local path file hash size
  local tracked_paths=(
    "AnToanUSB.exe"
    "K3 Mac.app/Contents/Info.plist"
    "K3 Mac.app/Contents/MacOS/K3UsbSafeMac"
    "tools/rules/k3-rules.json"
  )

  {
    printf '{\n'
    printf '  "version": 1,\n'
    printf '  "generated_at": "%s",\n' "$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
    printf '  "files": [\n'
    for path in "${tracked_paths[@]}"; do
      file="$target/$path"
      [ -f "$file" ] || continue
      hash="$(shasum -a 256 "$file" | awk '{print $1}')"
      size="$(stat -f %z "$file")"
      if [ "$first" -eq 0 ]; then
        printf ',\n'
      fi
      first=0
      printf '    {"path":"%s","sha256":"%s","size":%s}' "$path" "$hash" "$size"
    done
    printf '\n  ]\n'
    printf '}\n'
  } > "$manifest"
}

write_release_report() {
  local target="$1"
  local report="$target/K3_RELEASE_REPORT.txt"
  local app_bin="$target/K3 Mac.app/Contents/MacOS/K3UsbSafeMac"
  local rules="$target/tools/rules/k3-rules.json"
  local clam="$target/tools/mac-arm64/clamav/clamscan"
  local freshclam="$target/tools/mac-arm64/clamav/freshclam"
  local clam_db="$target/tools/mac-arm64/clamav/database/main.cvd"

  {
    printf 'USB An Toan K3 - Release Report\n'
    printf 'Generated: %s\n' "$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
    printf 'Target: %s\n\n' "$target"
    printf '[macOS]\n'
    if [ -f "$app_bin" ]; then
      printf 'K3 Mac.app: OK (%s bytes)\n' "$(stat -f %z "$app_bin")"
      printf 'K3 Mac SHA-256: %s\n' "$(shasum -a 256 "$app_bin" | awk '{print $1}')"
    else
      printf 'K3 Mac.app: MISSING\n'
    fi
    printf '\n[Rules]\n'
    if [ -f "$rules" ]; then
      printf 'k3-rules.json: OK (%s bytes)\n' "$(stat -f %z "$rules")"
    else
      printf 'k3-rules.json: MISSING\n'
    fi
    printf '\n[ClamAV portable]\n'
    [ -f "$clam" ] && printf 'clamscan: OK\n' || printf 'clamscan: MISSING\n'
    [ -f "$freshclam" ] && printf 'freshclam: OK\n' || printf 'freshclam: MISSING\n'
    [ -f "$clam_db" ] && printf 'database/main.cvd: OK (%s bytes)\n' "$(stat -f %z "$clam_db")" || printf 'database/main.cvd: MISSING\n'
    printf '\n[Integrity]\n'
    if [ -f "$target/.k3_integrity_manifest.json" ]; then
      printf '.k3_integrity_manifest.json: OK\n'
    else
      printf '.k3_integrity_manifest.json: MISSING\n'
    fi
    printf '\n[Data safety]\n'
    printf 'Vault/config data was not deleted by this script.\n'
  } > "$report"
}

echo "==> Preparing dist: $DIST_DIR"
mkdir -p "$DIST_DIR"
prepare_layout "$DIST_DIR"

if [ -n "$USB_TARGET" ]; then
  echo "==> Updating USB target: $USB_TARGET"
  prepare_layout "$USB_TARGET"
fi

echo "Done."
echo "Dist: $DIST_DIR"
if [ -n "$USB_TARGET" ]; then
  echo "USB:  $USB_TARGET"
fi
