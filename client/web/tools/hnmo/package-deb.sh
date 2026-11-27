#!/usr/bin/env bash
# Wrap the tested portable release in a Debian desktop installer.
set -euo pipefail

archive="${1:?Pass HNMO.zip}"
version="${2:?Pass the release version}"
output="${3:?Pass the output .deb path}"
tools="$(cd "$(dirname "$0")" && pwd)"
staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT
install -d "$staging"/{DEBIAN,usr/bin,usr/lib/hnmo,usr/share/applications,usr/share/icons/hicolor/512x512/apps}
install -m 0644 "$archive" "$staging/usr/lib/hnmo/HNMO.zip"
install -m 0755 "$tools/start-installed-linux.sh" "$staging/usr/lib/hnmo/start"
install -m 0755 "$tools/install-user-folder.sh" "$staging/usr/lib/hnmo/install-user-folder.sh"
ln -s ../lib/hnmo/start "$staging/usr/bin/hnmo"
install -m 0644 "$tools/Start HNMO.desktop" "$staging/usr/share/applications/hnmo.desktop"
install -m 0644 "$tools/../../public/images/icon-512.png" "$staging/usr/share/icons/hicolor/512x512/apps/hnmo.png"

cat > "$staging/DEBIAN/control" <<CONTROL
Package: hnmo
Version: $version
Section: games
Priority: optional
Architecture: all
Maintainer: HNMO
Depends: bash, unzip, xdg-utils, libicu74 | libicu72 | libicu70 | libicu-dev, libssl3t64 | libssl3, zlib1g
Description: HNMO desktop server launcher
 Start the server and play in your browser.
CONTROL
dpkg-deb --root-owner-group -Znone --build "$staging" "$output"
