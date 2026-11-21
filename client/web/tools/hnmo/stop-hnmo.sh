#!/usr/bin/env bash
# Stops HNMO started in the background by start-hnmo.sh: the game, the database and the launcher (as the admin panel's
# "Stop HNMO" does).
set -uo pipefail
cd "$(dirname "$0")"
port=$(sed -n 's/.*"WebPort"[[:space:]]*:[[:space:]]*\([0-9]*\).*/\1/p' save/hnmo-settings.json 2>/dev/null | head -1)
port=${port:-8080}
if curl -fsS --noproxy "*" -X POST "http://127.0.0.1:$port/api/control/shutdown" > /dev/null 2>&1; then
  echo "HNMO is stopping. / HNMO đang tắt."
elif [ -f save/hnmo.pid ] && kill "$(cat save/hnmo.pid)" 2> /dev/null; then
  echo "HNMO is stopping. / HNMO đang tắt."
else
  echo "HNMO doesn't seem to run. / HNMO có vẻ không chạy."
fi
rm -f save/hnmo.pid
