#!/usr/bin/env bash
set -euo pipefail

PACKAGE_DIR="${1:?Usage: build-dmg.sh <hnmo-folder> <output-dmg>}"
OUT_DMG="${2:?Usage: build-dmg.sh <hnmo-folder> <output-dmg>}"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

APP_DIR="$WORK_DIR/HNMO.app"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources/HNMO"
cp -R "$PACKAGE_DIR/." "$APP_DIR/Contents/Resources/HNMO/"

cat > "$APP_DIR/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>HNMO</string>
  <key>CFBundleDisplayName</key><string>HNMO</string>
  <key>CFBundleIdentifier</key><string>com.hnmo.launcher</string>
  <key>CFBundleVersion</key><string>1.0</string>
  <key>CFBundleExecutable</key><string>HNMO</string>
  <key>CFBundlePackageType</key><string>APPL</string>
</dict>
</plist>
PLIST

cat > "$APP_DIR/Contents/MacOS/HNMO" <<'SH'
#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../Resources/HNMO" && pwd)"
exec "$ROOT/start-hnmo.sh"
SH
chmod +x "$APP_DIR/Contents/MacOS/HNMO"

mkdir -p "$(dirname "$OUT_DMG")"
hdiutil create -volname "HNMO" -srcfolder "$WORK_DIR" -ov -format UDZO "$OUT_DMG"
echo "Created $OUT_DMG"
