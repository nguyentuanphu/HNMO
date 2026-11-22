#!/usr/bin/env bash
set -euo pipefail

# Uploads new/changed HNMO files (keyed by SHA-256) to Cloudflare R2 in parallel and prints progress.
# Usage: upload-r2-files.sh <src_dir> <manifest_json> <have_txt> <bucket> <endpoint>

SRC_DIR="${1:?src_dir required}"
MANIFEST_JSON="${2:?manifest_json required}"
HAVE_FILE="${3:?have_txt required}"
R2_BUCKET="${4:?bucket required}"
R2_ENDPOINT="${5:?endpoint required}"
PARALLEL="${R2_UPLOAD_PARALLEL:-8}"
PROGRESS_EVERY="${R2_PROGRESS_EVERY:-50}"
AWS_BIN="${AWS_CMD:-aws}"
NODE_BIN="${NODE_CMD:-node}"

UPLOAD_BIN="$(mktemp)"
trap 'rm -f "$UPLOAD_BIN"' EXIT

MANIFEST_ARG="$MANIFEST_JSON"
HAVE_ARG="$HAVE_FILE"
OUT_ARG="$UPLOAD_BIN"
if command -v cygpath >/dev/null 2>&1; then
  MANIFEST_ARG="$(cygpath -w "$MANIFEST_JSON")"
  HAVE_ARG="$(cygpath -w "$HAVE_FILE")"
  OUT_ARG="$(cygpath -w "$UPLOAD_BIN")"
fi

TOTAL="$("$NODE_BIN" -e '
const fs = require("fs");
const [manifestPath, havePath, outPath] = process.argv.slice(1);
const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
const have = new Set(fs.readFileSync(havePath, "utf8").split(/\s+/).filter(Boolean));
const seen = new Set();
const items = [];
for (const entry of manifest.files) {
  if (!have.has(entry.sha256) && !seen.has(entry.sha256)) {
    seen.add(entry.sha256);
    items.push([entry.sha256, entry.path]);
  }
}
const buf = Buffer.from(items.map(([sha, rel], i) => `${i + 1}\0${sha}\0${rel}\0`).join(""), "utf8");
fs.writeFileSync(outPath, buf);
process.stdout.write(String(items.length));
' "$MANIFEST_ARG" "$HAVE_ARG" "$OUT_ARG")"

echo "$TOTAL files to upload ($PARALLEL parallel)"
if [ "$TOTAL" -eq 0 ]; then
  exit 0
fi

upload_one() {
  local idx="$1" hash="$2" rel="$3"
  if ! "$AWS_BIN" s3 cp "$SRC_DIR/$rel" "s3://$R2_BUCKET/files/$hash" --endpoint-url "$R2_ENDPOINT" --only-show-errors; then
    echo "[R2 $idx/$TOTAL] FAILED $rel ($hash)" >&2
    return 255
  fi
  if [ "$idx" -eq 1 ] || [ "$idx" -eq "$TOTAL" ] || [ $((idx % PROGRESS_EVERY)) -eq 0 ]; then
    echo "[R2 $idx/$TOTAL] uploaded $rel ($hash)"
  fi
}

export SRC_DIR R2_BUCKET R2_ENDPOINT AWS_BIN TOTAL PROGRESS_EVERY
export -f upload_one

xargs -0 -r -P "${R2_UPLOAD_PARALLEL:-8}" -n 3 bash -euo pipefail -c 'upload_one "$1" "$2" "$3"' _ < "$UPLOAD_BIN"
