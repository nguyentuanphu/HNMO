#!/usr/bin/env bash
# macOS Finder double-click launcher: changes to the HNMO folder and runs start-hnmo.sh.
set -euo pipefail
cd "$(dirname "$0")"
exec ./start-hnmo.sh "$@"
