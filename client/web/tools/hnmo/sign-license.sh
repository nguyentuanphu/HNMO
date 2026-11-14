#!/usr/bin/env bash
# Signs a player license (license.hnmo) for one host, with the owner's private key (the same Ed25519 key
# as the build integrity manifest; never share it). Without a license an HNMO folder lets 10 players in
# at once; the license raises that.
#
# Usage: sign-license.sh <licensee> <max players> [expires YYYY-MM-DD|-] [machine id|-] [output file]
#   licensee     who it's for, e.g. "Anh Minh"
#   max players  players at once, e.g. 30
#   expires      last valid day (YYYY-MM-DD), or - for no end
#   machine id   bind it to one computer: the id the host's server writes to its log at startup
#                ("HNMO license: ... (this computer's id: 1a2b3c...)"), or - for any computer
#   output       default license.hnmo
# The private key comes from HNMO_SIGNING_KEY_FILE (a PEM file) or HNMO_SIGNING_KEY (its contents).
# The host puts license.hnmo next to "Start HNMO" (the folder's top) and restarts the game.
set -euo pipefail

LICENSEE="${1:?Who is the license for?}"
MAX="${2:?How many players at once?}"
EXPIRES="${3:--}"
MACHINE="${4:--}"
OUT="${5:-license.hnmo}"
[[ "$MAX" =~ ^[0-9]+$ ]] && [ "$MAX" -ge 1 ] || { echo "max players must be a number from 1" >&2; exit 1; }
[ "$EXPIRES" = "-" ] || [[ "$EXPIRES" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]] || { echo "expires must be YYYY-MM-DD or -" >&2; exit 1; }

KEY="${HNMO_SIGNING_KEY_FILE:-}"
TMP_KEY=""
if [ -z "$KEY" ]; then
  [ -n "${HNMO_SIGNING_KEY:-}" ] || { echo "Set HNMO_SIGNING_KEY_FILE (or HNMO_SIGNING_KEY) to the private signing key." >&2; exit 1; }
  TMP_KEY="$(mktemp)"; printf '%s' "$HNMO_SIGNING_KEY" > "$TMP_KEY"; KEY="$TMP_KEY"
fi
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"; [ -n "$TMP_KEY" ] && rm -f "$TMP_KEY"' EXIT

json_string() { python3 -c 'import json,sys; print(json.dumps(sys.argv[1], ensure_ascii=False))' "$1"; }
EXPIRES_JSON=$([ "$EXPIRES" = "-" ] && echo null || json_string "$EXPIRES")
MACHINE_JSON=$([ "$MACHINE" = "-" ] && echo null || json_string "$MACHINE")
printf '{"licensee":%s,"maxPlayers":%s,"expires":%s,"machine":%s,"issued":"%s"}' \
  "$(json_string "$LICENSEE")" "$MAX" "$EXPIRES_JSON" "$MACHINE_JSON" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$WORK/payload.json"
openssl pkeyutl -sign -inkey "$KEY" -rawin -in "$WORK/payload.json" -out "$WORK/signature.bin"
printf '{\n  "payload": "%s",\n  "signature": "%s"\n}\n' "$(base64 -w0 "$WORK/payload.json")" "$(base64 -w0 "$WORK/signature.bin")" > "$OUT"
echo "Signed $OUT: $(cat "$WORK/payload.json")"
