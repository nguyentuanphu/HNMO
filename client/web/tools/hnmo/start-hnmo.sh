#!/usr/bin/env bash
# Starts HNMO: the save game database, the game server with its admin panel, and the
# game, which opens in your browser.
# Silent background mode by default (owner's choice: no Terminal window stays open); stop with
# ./stop-hnmo.sh or "Stop HNMO" in the admin panel / control page.
# Visible console mode: pass --foreground (or --window / --console) or set "ConsoleWindow": true
# in save/hnmo-settings.json (or HNMO_FOREGROUND=1 / HNMO_WINDOW=1).
set -euo pipefail
cd "$(dirname "$0")"
if [ ! -d Data/Monsters ] && [ ! -d Data/packs ]; then
  echo "The game data is missing. Copy the Data folder of the game into $(pwd)/Data."
  exit 1
fi

foreground="${HNMO_FOREGROUND:-${HNMO_WINDOW:-}}"
for arg in "$@"; do
  case "$arg" in
    --foreground|--window|--console) foreground=1 ;;
  esac
done
if [ -z "$foreground" ] && [ -f save/hnmo-settings.json ] && grep -qiE '"ConsoleWindow"[[:space:]]*:[[:space:]]*true' save/hnmo-settings.json 2>/dev/null; then
  foreground=1
fi

if [ -z "$foreground" ]; then
  mkdir -p save/logs
  if [ -f save/hnmo.pid ]; then
    existing_pid="$(tr -d '[:space:]' < save/hnmo.pid 2>/dev/null || true)"
    if [ -n "$existing_pid" ] && kill -0 "$existing_pid" 2>/dev/null; then
      echo "HNMO is already running (PID $existing_pid). Opening browser or run ./stop-hnmo.sh to stop it."
      echo "HNMO đang chạy sẵn (PID $existing_pid). Dùng ./stop-hnmo.sh nếu muốn tắt."
      port="$(sed -n 's/.*"WebPort"[[:space:]]*:[[:space:]]*\([0-9]*\).*/\1/p' save/hnmo-settings.json 2>/dev/null | head -1)"
      port="${port:-8080}"
      if command -v open >/dev/null 2>&1; then open "http://localhost:$port/control" >/dev/null 2>&1 || true
      elif command -v xdg-open >/dev/null 2>&1; then xdg-open "http://localhost:$port/control" >/dev/null 2>&1 || true; fi
      exit 0
    fi
  fi
  HNMO_FOREGROUND=1 nohup "$0" --foreground > save/logs/console.log 2>&1 &
  echo $! > save/hnmo.pid
  echo "HNMO is starting in the background; the game opens in your browser."
  echo "Log: the admin panel's \"Server log\" (or save/logs). Stop: \"Stop HNMO\" there, or ./stop-hnmo.sh."
  echo "HNMO đang khởi động chạy nền; game sẽ mở trong trình duyệt. Tắt: \"Tắt HNMO\" trong admin panel, hoặc ./stop-hnmo.sh."
  exit 0
fi

cd web
# The folder for every system: the .NET runtime of this computer's system (runtime/<system>).
platform="$(uname -s)"
machine="$(uname -m)"
# A translated shell on Apple Silicon reports x86_64. Select the hardware runtime
# so both the launcher and its PostgreSQL child start as native processes.
if [ "$platform" = Darwin ] && [ "$(sysctl -n hw.optional.arm64 2>/dev/null || true)" = 1 ]; then
  machine=arm64
fi
case "$platform-$machine" in
  Linux-x86_64) rid=linux-x64 ;;
  Linux-aarch64|Linux-arm64) rid=linux-arm64 ;;
  Darwin-arm64) rid=osx-arm64 ;;
  Darwin-x86_64) rid=osx-x64 ;;
  *) rid="" ;;
esac
if [ -f HNMO.dll ]; then
  if [ -n "$rid" ] && [ -x "../runtime/$rid/dotnet" ]; then exec "../runtime/$rid/dotnet" HNMO.dll; fi
  if command -v dotnet >/dev/null; then exec dotnet HNMO.dll; fi
  echo "No .NET runtime for $(uname -s) $(uname -m) in this folder (runtime/). Install .NET 10 (https://dot.net) and start again."
  exit 1
fi
exec ./HNMO
