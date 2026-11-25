#!/usr/bin/env bash
set -euo pipefail

package_dir="$(dirname "$(readlink -f "$0")")"
data_dir="${XDG_DATA_HOME:-$HOME/.local/share}"
install_dir="$data_dir/HNMO"
"$package_dir/install-user-folder.sh" "$package_dir/HNMO.zip" "$install_dir"
exec "$install_dir/start-hnmo.sh" "$@"
