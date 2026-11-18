#!/usr/bin/env bash
# Starts HNMO: the save game database, the game server with its admin panel, and the
# game, which opens in your browser. Press Ctrl+C to stop everything.
set -euo pipefail
cd "$(dirname "$0")"
if [ ! -d Data/Monsters ] && [ ! -d Data/packs ]; then
  echo "The game data is missing. Copy the Data folder of the game into $(pwd)/Data."
  exit 1
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
