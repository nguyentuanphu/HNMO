#!/usr/bin/env bash
# Extract a fresh HNMO installation without replacing an existing save.
set -euo pipefail

archive="${1:?Pass the bundled HNMO.zip}"
target="${2:?Pass the user installation path}"
if [ -x "$target/start-hnmo.sh" ]; then exit 0; fi

parent="$(dirname "$target")"
mkdir -p "$parent"
lock="$parent/.hnmo-install-lock"
if ! mkdir "$lock" 2>/dev/null; then
  echo "HNMO is already being installed. / HNMO đang được cài đặt." >&2
  exit 1
fi
trap 'rm -rf "$lock"' EXIT
if [ -x "$target/start-hnmo.sh" ]; then exit 0; fi
if [ -e "$target" ] || [ -L "$target" ]; then
  echo "The HNMO folder already exists. / Thư mục HNMO đã tồn tại." >&2
  exit 1
fi

unzip -q "$archive" -d "$lock"
test -x "$lock/HNMO/start-hnmo.sh"
mv -- "$lock/HNMO" "$target"
