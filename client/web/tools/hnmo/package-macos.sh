#!/usr/bin/env bash
# A standalone Finder application, with its portable HNMO folder inside.
set -euo pipefail

archive="${1:?Pass HNMO.zip}"
output="${2:?Pass the output ZIP path}"
output="$(cd "$(dirname "$output")" && pwd)/$(basename "$output")"
tools="$(cd "$(dirname "$0")" && pwd)"
archive="$(cd "$(dirname "$archive")" && pwd)/$(basename "$archive")"
staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT
unzip -q "$archive" 'HNMO/Start HNMO.app/*' -d "$staging"
mv "$staging/HNMO/Start HNMO.app" "$staging/"
resources="$staging/Start HNMO.app/Contents/Resources"
install -d "$resources"
install -m 0644 "$archive" "$resources/HNMO.zip"
install -m 0755 "$tools/install-user-folder.sh" "$resources/install-user-folder.sh"
(cd "$staging" && zip -0qr "$output" 'Start HNMO.app')
