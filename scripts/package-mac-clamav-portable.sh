#!/usr/bin/env bash
set -euo pipefail

USB_ROOT="${1:-/Volumes/USB-DATA}"

if [[ ! -d "$USB_ROOT" ]]; then
  echo "USB root khong ton tai: $USB_ROOT" >&2
  exit 1
fi

ARCH="$(uname -m)"
case "$ARCH" in
  arm64) TARGET_DIR="$USB_ROOT/tools/mac-arm64/clamav" ;;
  x86_64) TARGET_DIR="$USB_ROOT/tools/mac-x64/clamav" ;;
  *)
    echo "Kien truc macOS chua ho tro: $ARCH" >&2
    exit 1
    ;;
esac

if ! command -v brew >/dev/null 2>&1; then
  echo "May dong goi can co Homebrew de lay ClamAV Mac." >&2
  exit 1
fi

if ! brew list --versions clamav >/dev/null 2>&1; then
  echo "Chua co clamav trong Homebrew. Hay chay: brew install clamav" >&2
  exit 1
fi

BREW_PREFIX="$(brew --prefix)"
CLAMAV_PREFIX="$(brew --prefix clamav)"

mkdir -p "$TARGET_DIR/bin" "$TARGET_DIR/lib" "$TARGET_DIR/database"

copy_file() {
  local src="$1"
  local dst="$2"
  if [[ -e "$src" ]]; then
    cp -f "$src" "$dst"
    chmod +x "$dst" 2>/dev/null || true
  fi
}

copy_dylib_tree() {
  local binary="$1"
  local depth="${2:-0}"
  if [[ "$depth" -gt 8 || ! -e "$binary" ]]; then
    return
  fi

  otool -L "$binary" 2>/dev/null | awk 'NR > 1 { print $1 }' | while read -r dep; do
    case "$dep" in
      "$BREW_PREFIX"/*|"$CLAMAV_PREFIX"/*)
        local base
        base="$(basename "$dep")"
        if [[ ! -e "$TARGET_DIR/lib/$base" ]]; then
          cp -f "$dep" "$TARGET_DIR/lib/$base"
          chmod +x "$TARGET_DIR/lib/$base" 2>/dev/null || true
          copy_dylib_tree "$dep" "$((depth + 1))"
        fi
        ;;
    esac
  done
}

for tool in clamscan freshclam sigtool clamconf; do
  src="$CLAMAV_PREFIX/bin/$tool"
  if [[ -x "$src" ]]; then
    copy_file "$src" "$TARGET_DIR/bin/$tool"
    copy_dylib_tree "$src" 0
  fi
done

for dylib in "$CLAMAV_PREFIX"/lib/*.dylib "$BREW_PREFIX"/opt/*/lib/*.dylib; do
  [[ -e "$dylib" ]] || continue
  name="$(basename "$dylib")"
  case "$name" in
    libclam*|libfreshclam*|libjson*|libcurl*|libssl*|libcrypto*|libxml2*|libpcre2*|libbz2*|libzstd*|liblzma*|libiconv*|libnghttp2*|libssh2*|libgmp*)
      cp -f "$dylib" "$TARGET_DIR/lib/$name"
      chmod +x "$TARGET_DIR/lib/$name" 2>/dev/null || true
      ;;
  esac
done

cat > "$TARGET_DIR/clamscan" <<'EOF'
#!/bin/zsh
DIR="$(cd "$(dirname "$0")" && pwd)"
export DYLD_LIBRARY_PATH="$DIR/lib:${DYLD_LIBRARY_PATH:-}"
exec "$DIR/bin/clamscan" "$@"
EOF

cat > "$TARGET_DIR/freshclam" <<'EOF'
#!/bin/zsh
DIR="$(cd "$(dirname "$0")" && pwd)"
cat > "$DIR/freshclam.conf" <<CONFIG
DatabaseDirectory $DIR/database
DatabaseMirror database.clamav.net
UpdateLogFile $DIR/freshclam.log
LogTime yes
CONFIG
export DYLD_LIBRARY_PATH="$DIR/lib:${DYLD_LIBRARY_PATH:-}"
cd "$DIR"
exec "$DIR/bin/freshclam" "$@"
EOF

cat > "$TARGET_DIR/freshclam.conf" <<'EOF'
DatabaseDirectory database
DatabaseMirror database.clamav.net
UpdateLogFile freshclam.log
LogTime yes
EOF

chmod +x "$TARGET_DIR/clamscan" "$TARGET_DIR/freshclam"

echo "Da dong goi ClamAV portable vao: $TARGET_DIR"
echo "Hay chay trong app K3: Diet virus > Cap nhat DB, hoac:"
echo "  cd '$TARGET_DIR' && ./freshclam --config-file=freshclam.conf --stdout"
