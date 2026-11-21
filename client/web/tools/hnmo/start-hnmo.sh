#!/usr/bin/env bash
# Starts HNMO: the save game database, the game server with its admin panel, and the
# game, which opens in your browser.
# In the background by default (owner's choice, 2026-11-21: no console window to keep open): the Terminal can be
# closed; the log is in the admin panel ("Server log") and save/logs; stop HNMO there ("Stop HNMO") or with
# ./stop-hnmo.sh. "./start-hnmo.sh --foreground" runs it in this Terminal as before (Ctrl+C stops it).
set -euo pipefail
cd "$(dirname "$0")"
if [ ! -d Data/Monsters ] && [ ! -d Data/packs ]; then
  echo "The game data is missing. Copy the Data folder of the game into $(pwd)/Data."
  exit 1
fi
if [ "${1:-}" != "--foreground" ] && [ -z "${HNMO_FOREGROUND:-}" ]; then
  mkdir -p save/logs
  HNMO_FOREGROUND=1 nohup "$0" --foreground > save/logs/console.log 2>&1 &
  echo $! > save/hnmo.pid
  echo "HNMO is starting in the background; the game opens in your browser."
  echo "Log: the admin panel's \"Server log\" (or save/logs). Stop: \"Stop HNMO\" there, or ./stop-hnmo.sh."
  echo "HNMO đang khởi động chạy nền; game sẽ mở trong trình duyệt. Tắt: \"Tắt HNMO\" trong admin panel, hoặc ./stop-hnmo.sh."
  exit 0
fi
cd web
# The folder for every system: the .NET runtime of this computer's system (runtime/<system>).
case "$(uname -s)-$(uname -m)" in
  Linux-x86_64) rid=linux-x64 ;;
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
