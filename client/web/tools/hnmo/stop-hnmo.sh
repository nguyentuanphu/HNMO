#!/usr/bin/env bash
# Stops HNMO started in the background by start-hnmo.sh: the web launcher, the game server, and the database.
set -uo pipefail
cd "$(dirname "$0")"

read_port() {
  local p=""
  if [ -f save/hnmo-settings.json ]; then
    p="$(sed -n 's/.*"WebPort"[[:space:]]*:[[:space:]]*\([0-9]*\).*/\1/p' save/hnmo-settings.json 2>/dev/null | head -1)"
  fi
  if [ -z "$p" ] && [ -f web/appsettings.json ]; then
    p="$(sed -n 's/.*"Urls"[^0-9]*:\([0-9]*\).*/\1/p' web/appsettings.json 2>/dev/null | head -1)"
  fi
  printf '%s\n' "${p:-8080}"
}

pid_alive() {
  local f="$1"
  [ -f "$f" ] || return 1
  local p
  p="$(tr -d '[:space:]' < "$f" 2>/dev/null || true)"
  [ -n "$p" ] && kill -0 "$p" 2>/dev/null
}

port="$(read_port)"
stopped=0
if command -v curl >/dev/null 2>&1 && curl -fsS --noproxy "*" --max-time 8 -X POST "http://127.0.0.1:$port/api/control/shutdown" >/dev/null 2>&1; then
  stopped=1
  for _ in $(seq 1 20); do
    if ! pid_alive save/hnmo.pid && ! pid_alive save/server.pid; then break; fi
    sleep 0.5
  done
fi

for pidfile in save/hnmo.pid save/server.pid; do
  if [ -f "$pidfile" ]; then
    pid="$(awk '{print $1}' "$pidfile" 2>/dev/null || true)"
    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
      kill -TERM "$pid" 2>/dev/null || true
      stopped=1
      for _ in $(seq 1 10); do
        kill -0 "$pid" 2>/dev/null || break
        sleep 0.4
      done
      if kill -0 "$pid" 2>/dev/null; then
        kill -KILL "$pid" 2>/dev/null || true
      fi
    fi
  fi
done

if [ -f save/db/postmaster.pid ]; then
  if [ -x postgres/bin/pg_ctl ]; then
    postgres/bin/pg_ctl stop -D save/db -m fast -w -t 8 >/dev/null 2>&1 || true
  elif command -v pg_ctl >/dev/null 2>&1; then
    pg_ctl stop -D save/db -m fast -w -t 8 >/dev/null 2>&1 || true
  fi
fi

rm -f save/hnmo.pid save/server.pid
if [ "$stopped" = 1 ]; then
  echo "HNMO has stopped. / HNMO đã tắt."
else
  echo "HNMO doesn't seem to run. / HNMO có vẻ không chạy."
fi
