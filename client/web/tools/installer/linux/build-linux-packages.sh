#!/usr/bin/env bash
set -euo pipefail

PACKAGE_DIR="${1:?Usage: build-linux-packages.sh <hnmo-folder> <out-dir>}"
OUT_DIR="${2:?Usage: build-linux-packages.sh <hnmo-folder> <out-dir>}"
mkdir -p "$OUT_DIR"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

# 1. Debian package (.deb)
DEB_ROOT="$WORK_DIR/deb"
mkdir -p "$DEB_ROOT/DEBIAN" "$DEB_ROOT/opt/hnmo" "$DEB_ROOT/usr/bin" "$DEB_ROOT/usr/share/applications"
cp -a "$PACKAGE_DIR/." "$DEB_ROOT/opt/hnmo/"

cat > "$DEB_ROOT/DEBIAN/control" <<'CTRL'
Package: hnmo
Version: 1.0.0
Section: games
Priority: optional
Architecture: amd64
Maintainer: HNMO <noreply@example.com>
Description: HNMO game server, admin panel, and web client
CTRL

cat > "$DEB_ROOT/usr/bin/hnmo" <<'SH'
#!/usr/bin/env bash
exec /opt/hnmo/start-hnmo.sh "$@"
SH
chmod +x "$DEB_ROOT/usr/bin/hnmo"

cat > "$DEB_ROOT/usr/share/applications/hnmo.desktop" <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=HNMO
Comment=Start HNMO server and web game
Exec=/usr/bin/hnmo
Terminal=true
Categories=Game;
DESKTOP

dpkg-deb --build --root-owner-group "$DEB_ROOT" "$OUT_DIR/HNMO-linux-x64.deb"
echo "Created $OUT_DIR/HNMO-linux-x64.deb"

# 2. AppImage (if appimagetool is available)
if command -v appimagetool >/dev/null; then
  APPDIR="$WORK_DIR/HNMO.AppDir"
  mkdir -p "$APPDIR/usr/share/hnmo"
  cp -a "$PACKAGE_DIR/." "$APPDIR/usr/share/hnmo/"
  cat > "$APPDIR/AppRun" <<'APPRUN'
#!/usr/bin/env bash
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/share/hnmo/start-hnmo.sh" "$@"
APPRUN
  chmod +x "$APPDIR/AppRun"
  cat > "$APPDIR/hnmo.desktop" <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=HNMO
Exec=AppRun
Icon=hnmo
Categories=Game;
DESKTOP
  printf '\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15c4\x00\x00\x00\nIDATx\x9cc\x00\x01\x00\x00\x05\x00\x01\r\n-\xb4\x00\x00\x00\x00IEND\xaeB`\x82' > "$APPDIR/hnmo.png"
  ARCH=x86_64 appimagetool "$APPDIR" "$OUT_DIR/HNMO-linux-x64.AppImage"
  echo "Created $OUT_DIR/HNMO-linux-x64.AppImage"
fi
