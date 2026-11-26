#!/usr/bin/env bash
# A standalone Finder application, with its portable HNMO folder inside.
set -euo pipefail

archive="${1:?Pass HNMO.zip}"
output="${2:?Pass the output ZIP path}"
launcher="${3:?Pass the universal Mach-O launcher}"
output="$(cd "$(dirname "$output")" && pwd)/$(basename "$output")"
tools="$(cd "$(dirname "$0")" && pwd)"
staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT
app="$staging/Start HNMO.app"
cp -R "$tools/Start HNMO.app" "$app"
entry="$app/Contents/MacOS/start"
resources="$app/Contents/Resources"
install -d "$resources"
# The start script goes to Resources: a script left in Contents/MacOS is treated as code and cannot be sealed.
mv "$entry" "$resources/start.sh"
chmod 0755 "$resources/start.sh"
install -m 0755 "$launcher" "$entry"
install -m 0644 "$archive" "$resources/HNMO.zip"
install -m 0755 "$tools/install-user-folder.sh" "$resources/install-user-folder.sh"
# Seal the assembled app, including the ZIP and scripts in Resources.
codesign --force --sign - "$app"
codesign --verify --strict "$app"
ditto -c -k --sequesterRsrc --keepParent "$app" "$output"
