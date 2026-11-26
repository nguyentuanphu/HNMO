#!/usr/bin/env bash
# Verify startup through the two desktop entry points using fresh saves.
set -euo pipefail
artifacts="${1:?Pass the artifact directory}"
scratch="$(mktemp -d "${RUNNER_TEMP:-/tmp}/HNMO (test).XXXXXX")"

if [ "$(uname -s)" = Darwin ]; then
  folder="$HOME/Library/Application Support/HNMO"
  test ! -e "$folder"
else
  export XDG_DATA_HOME="$scratch/user"
  folder="$XDG_DATA_HOME/HNMO"
fi

cleanup() {
  local result=$?
  if [ "$result" != 0 ] && [ -d "$folder/save/logs" ]; then
    for log in "$folder"/save/logs/*.log; do
      if [ -f "$log" ]; then tail -n 50 "$log"; fi
    done
  fi
  if [ -f "$folder/stop-hnmo.sh" ]; then "$folder/stop-hnmo.sh"; fi
}
trap cleanup EXIT

if [ "$(uname -s)" = Darwin ]; then
  ditto -x -k "$artifacts/HNMO-macos.zip" "$scratch"
  lipo -verify_arch arm64 x86_64 "$scratch/Start HNMO.app/Contents/MacOS/start"
  codesign --verify "$scratch/Start HNMO.app/Contents/MacOS/start"
  open "$scratch/Start HNMO.app"
else
  dpkg-deb -x "$artifacts/HNMO-linux.deb" "$scratch/package"
  desktop-file-validate "$scratch/package/usr/share/applications/hnmo.desktop"
  PATH="$scratch/package/usr/bin:$PATH" gio launch "$scratch/package/usr/share/applications/hnmo.desktop"
fi

deadline=$((SECONDS + 720))
until curl --max-time 3 -fsS http://127.0.0.1:8080/index.html -o /dev/null &&
      curl --max-time 3 -fsS http://127.0.0.1:8095/ -o /dev/null &&
      [ -f "$folder/save/database/PG_VERSION" ]; do
  if [ "$SECONDS" -ge "$deadline" ]; then echo "HNMO did not start" >&2; exit 1; fi
  sleep 2
done

test "$(cat "$folder/save/database/PG_VERSION")" = 16
case "$(uname -s)-$(uname -m)" in
  Darwin-arm64) rid=osx-arm64 ;;
  Darwin-x86_64) rid=osx-x64 ;;
  Linux-aarch64) rid=linux-arm64 ;;
  Linux-x86_64) rid=linux-x64 ;;
  *) echo 'Unsupported test runner' >&2; exit 1 ;;
esac
grep -F "postgres/$rid/bin" "$folder/save/logs/launcher.log"
"$folder/postgres/$rid/bin/pg_ctl" status -D "$folder/save/database"
"$folder/postgres/$rid/bin/pg_dump" --version
echo "HNMO game, admin panel and bundled database started on $rid."
