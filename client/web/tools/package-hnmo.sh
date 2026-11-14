#!/usr/bin/env bash
# Builds the HNMO folder: the game server with its admin panel, the web client and
# HNMO.Web (web server + relay), ready to start with one click. Nothing has to be
# installed on the computer that runs it: .NET is included.
#
# Usage: web/tools/package-hnmo.sh <path to the server source (the HNMO server)> [runtime] [output folder] [--no-data]
#   runtime: win-x64 (default), linux-x64, osx-arm64, ...
#   --no-data: leave the game data out (copy src/bin/Data into HNMO/Data yourself)
# Environment:
#   POSTGRES_DIR: a portable PostgreSQL (with bin/, lib/, share/) to put into the folder,
#     e.g. the extracted Windows binaries zip of EnterpriseDB. Without it, the folder
#     uses POSTGRES_BIN (default /usr/lib/postgresql/16/bin), a PostgreSQL installed on the computer.
set -euo pipefail

SERVER_SOURCE="${1:?Give the path of the server source (the server folder of HNMO)}"
RUNTIME="${2:-win-x64}"
WEB="$(cd "$(dirname "$0")/.." && pwd)"
CLIENT_REPO="$(cd "$WEB/.." && pwd)"
OUT="${3:-$CLIENT_REPO/dist/HNMO-$RUNTIME}"
WITH_DATA=1
for arg in "$@"; do [ "$arg" = "--no-data" ] && WITH_DATA=0; done

echo "Building HNMO for $RUNTIME into $OUT"
rm -rf "$OUT"
mkdir -p "$OUT"

# The game server as a single self-contained executable (plus the Blazor static-web-assets
# manifest, wwwroot and a couple of config files), instead of hundreds of loose DLLs. Plugin
# discovery scans the assemblies loaded into the process (AppDomain.GetAssemblies), which load
# transparently from the bundle, so [PlugIn] discovery and the admin panel keep working.
# .pdb / .xml are dropped to keep the folder small.
dotnet publish "$SERVER_SOURCE/src/Startup/HNMO.Startup.csproj" -c Release -r "$RUNTIME" --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none -p:DebugSymbols=false -p:GenerateDocumentationFile=false \
  -p:AllowedReferenceRelatedFileExtensions=.no-related-files \
  -o "$OUT/server" -v quiet
dotnet publish "$WEB/bridge/HNMO.Web.csproj" -c Release -r "$RUNTIME" --self-contained -o "$OUT/web" -v quiet -p:HnmoServerSource="$SERVER_SOURCE"

# The web client (TypeScript, built and type-checked here; a type error stops the build) and its library
echo "Building the web client"
(cd "$WEB" && rm -rf public/js && npx tsc -p .)
mkdir -p "$OUT/web/wwwroot" "$OUT/web/vendor/three"
cp -r "$WEB/public/." "$OUT/web/wwwroot/"
find "$OUT/web/wwwroot" -name '*.js.map' -delete
cp -r "$WEB/node_modules/three/build" "$OUT/web/vendor/three/build"

# Bundle + minify the web client into a single file for the package (dev keeps the
# unbundled public/js). three stays external (resolved by the page's import map), and
# the Data files stay separate. The individual modules are then dropped from the folder.
echo "Bundling the web client (esbuild)"
npx --prefix "$WEB" esbuild "$OUT/web/wwwroot/js/main.js" \
  --bundle --minify --format=esm --external:three \
  --outfile="$OUT/web/wwwroot/js/main.min.js" --log-level=warning
find "$OUT/web/wwwroot/js" -maxdepth 1 -name '*.js' ! -name 'main.min.js' -delete
# Point the packaged page at the bundle (dev index.html is untouched).
sed -i 's#src="/js/main.js"#src="/js/main.min.js"#' "$OUT/web/wwwroot/index.html"

# The save game database: bundled, or one installed on the computer
POSTGRES_BIN_IN_FOLDER="../postgres/bin"
if [ -n "${POSTGRES_DIR:-}" ]; then
  mkdir -p "$OUT/postgres"
  for part in bin lib share; do cp -r "$POSTGRES_DIR/$part" "$OUT/postgres/$part"; done
else
  POSTGRES_BIN_IN_FOLDER="${POSTGRES_BIN:-/usr/lib/postgresql/16/bin}"
fi

# The game server talks to the folder's own database on its own port
sed -i 's/Port=5432;/Port=55432;/g' "$OUT/server/ConnectionSettings.xml"

cat > "$OUT/web/appsettings.json" <<JSON
{
  "Urls": "http://0.0.0.0:8080",
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "HNMO": { "WebRoot": "wwwroot", "DataPath": "../Data", "VendorPath": "vendor" },
  "Relay": { "AllowedHosts": [ "127.0.0.1", "localhost" ], "AllowLoopback": true, "ConnectTimeoutSeconds": 10 },
  "Launcher": {
    "Enabled": true,
    "PostgresPath": "$POSTGRES_BIN_IN_FOLDER",
    "SavePath": "../save",
    "DatabasePort": 55432,
    "ServerPath": "../server",
    "AdminUrl": "http://127.0.0.1:8095",
    "OpenBrowserUrl": "http://localhost:8080/"
  }
}
JSON

if [ "$WITH_DATA" = 1 ]; then
  echo "Copying the game data"
  cp -r "$CLIENT_REPO/src/bin/Data" "$OUT/Data"
else
  mkdir -p "$OUT/Data"
fi

mkdir -p "$OUT/licenses"
cp "$SERVER_SOURCE/licenses/LICENSE.txt" "$OUT/licenses/server-LICENSE.txt"
cp "$WEB/node_modules/three/LICENSE" "$OUT/licenses/three.js-LICENSE.txt"
if [ -n "${POSTGRES_DIR:-}" ]; then
  echo "PostgreSQL is released under the PostgreSQL License: https://www.postgresql.org/about/licence/" > "$OUT/licenses/postgresql-LICENSE.txt"
fi
cp "$WEB/tools/hnmo/README.txt" "$OUT/README.txt"
cp "$WEB/tools/hnmo/start-hnmo.sh" "$WEB/tools/hnmo/Start HNMO.bat" "$OUT/"
chmod +x "$OUT/start-hnmo.sh"

# HNMO ownership watermark. The marker locations are documented in the (unshipped)
# note client/web/tools/hnmo/WATERMARKS.private.md. See also PROTECTION.md.
REPO_ROOT="$(cd "$CLIENT_REPO/.." && pwd)"
PROJECT_ID="$(cat "$REPO_ROOT/PROJECT_ID.txt")"
STAMP="HNMO-$PROJECT_ID-$(date -u +%Y%m%d)"
# 1) A hidden dot-file at a non-obvious path.
printf '%s\n' "$STAMP" > "$OUT/web/wwwroot/.hnmo-build"
# 2) A few bytes appended after a newline at the end of an existing shipped file
#    (an HTML comment is not rendered, so it stays out of sight but travels with the build).
printf '\n<!-- %s -->\n' "$STAMP" >> "$OUT/web/wwwroot/index.html"

# Tamper-evident integrity manifest: hash the key files and sign the manifest with the
# private Ed25519 key (never shipped). The server re-hashes and verifies at startup against
# the embedded public key. The private key comes from HNMO_SIGNING_KEY (PEM contents) or
# HNMO_SIGNING_KEY_FILE (path); with neither, the build is UNSIGNED (a dev build).
SIGN_KEY_FILE=""
TMP_KEY=""
if [ -n "${HNMO_SIGNING_KEY_FILE:-}" ]; then
  SIGN_KEY_FILE="$HNMO_SIGNING_KEY_FILE"
elif [ -n "${HNMO_SIGNING_KEY:-}" ]; then
  TMP_KEY="$(mktemp)"
  printf '%s' "$HNMO_SIGNING_KEY" > "$TMP_KEY"
  SIGN_KEY_FILE="$TMP_KEY"
fi

if [ -n "$SIGN_KEY_FILE" ]; then
  echo "Signing the integrity manifest"
  (
    cd "$OUT"
    MANIFEST_FILES=()
    # The single-file server executable (and any loose HNMO.*.dll, e.g. a non-single-file build).
    for exe in server/HNMO.Startup server/HNMO.Startup.exe; do
      [ -f "$exe" ] && MANIFEST_FILES+=("$exe")
    done
    while IFS= read -r f; do MANIFEST_FILES+=("$f"); done < <(find server -maxdepth 1 -name 'HNMO.*.dll' | sort)
    for launcher in web/HNMO.Web web/HNMO.Web.exe; do
      [ -f "$launcher" ] && MANIFEST_FILES+=("$launcher")
    done
    for bundle in web/wwwroot/js/main.min.js web/wwwroot/js/main.js; do
      if [ -f "$bundle" ]; then MANIFEST_FILES+=("$bundle"); break; fi
    done

    {
      printf '{\n'
      printf '  "projectId": "%s",\n' "$PROJECT_ID"
      printf '  "buildStamp": "%s",\n' "$STAMP"
      printf '  "created": "%s",\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
      printf '  "algorithm": "SHA-256",\n'
      printf '  "files": [\n'
      last=$(( ${#MANIFEST_FILES[@]} - 1 ))
      for i in "${!MANIFEST_FILES[@]}"; do
        f="${MANIFEST_FILES[$i]}"
        h="$(sha256sum "$f" | cut -d' ' -f1)"
        sep=","
        [ "$i" -eq "$last" ] && sep=""
        printf '    { "path": "%s", "sha256": "%s" }%s\n' "$f" "$h" "$sep"
      done
      printf '  ]\n'
      printf '}\n'
    } > server/HNMO.integrity.json

    openssl pkeyutl -sign -inkey "$SIGN_KEY_FILE" -rawin -in server/HNMO.integrity.json -out /tmp/hnmo-integrity.sig.bin
    base64 -w0 /tmp/hnmo-integrity.sig.bin > server/HNMO.integrity.sig
    rm -f /tmp/hnmo-integrity.sig.bin
    echo "Signed ${#MANIFEST_FILES[@]} file(s) into server/HNMO.integrity.json"
  )
  [ -n "$TMP_KEY" ] && rm -f "$TMP_KEY"
else
  echo "WARNING: no signing key (HNMO_SIGNING_KEY / HNMO_SIGNING_KEY_FILE) — this is an UNSIGNED dev build; the server will start with an integrity warning."
fi

echo "Done: $OUT"
