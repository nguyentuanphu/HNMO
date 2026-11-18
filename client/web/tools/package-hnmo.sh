#!/usr/bin/env bash
# Builds the HNMO folder: the game server with its admin panel, the web client and
# HNMO.Web (web server + relay), ready to start with one click. Nothing has to be
# installed on the computer that runs it: .NET is included.
#
# Usage: web/tools/package-hnmo.sh <path to the server source (the HNMO server)> [runtime] [output folder] [--no-data]
#   runtime: portable (one folder for Windows, Linux and macOS), or one system: win-x64 (default), linux-x64, osx-arm64, ...
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

if [ "$RUNTIME" = portable ]; then
  # One folder for every system: the server and the web server as portable .NET programs (built once),
  # and Microsoft's .NET runtime for each system next to them (runtime/<system>). The start files pick
  # the runtime of the computer they run on; the web server starts the game server with the same one.
  # Lighter than a self-contained build per system (that carries the runtime twice: server and web).
  # (No UseAppHost=false here: it would reach the build's own code generator, which needs its app host.
  #  The two app hosts of this build machine's system are removed below instead.)
  dotnet publish "$SERVER_SOURCE/src/Startup/HNMO.Startup.csproj" -c Release --no-self-contained \
    -p:DebugType=none -p:DebugSymbols=false -p:GenerateDocumentationFile=false \
    -p:AllowedReferenceRelatedFileExtensions=.no-related-files \
    -o "$OUT/server" -v quiet
  dotnet publish "$WEB/bridge/HNMO.Web.csproj" -c Release --no-self-contained -o "$OUT/web" -v quiet \
    -p:HnmoServerSource="$SERVER_SOURCE" -p:DebugType=none -p:DebugSymbols=false -p:GenerateDocumentationFile=false \
    -p:AllowedReferenceRelatedFileExtensions=.no-related-files
  rm -f "$OUT/server/HNMO.Startup" "$OUT/server/HNMO.Startup.exe" "$OUT/web/HNMO" "$OUT/web/HNMO.exe"
  # Debug symbols and the code documentation (HNMO.*.xml) aren't needed to run.
  find "$OUT/server" "$OUT/web" -maxdepth 1 \( -name "*.pdb" -o -name "HNMO.*.xml" \) -delete
  # The newest installed runtime of the version HNMO is built for (net10.0 -> 10.x), not simply the
  # newest one: a newer major version (11) doesn't run it.
  NET_MAJOR="$(sed -n 's#.*<TargetFramework>net\([0-9]*\)\..*#\1#p' "$WEB/bridge/HNMO.Web.csproj" | head -1)"
  ASPNET_VERSION="${HNMO_RUNTIME_VERSION:-$(dotnet --list-runtimes | awk -v major="$NET_MAJOR" '$1 == "Microsoft.AspNetCore.App" && index($2, major ".") == 1 {v=$2} END{print v}')}"
  if [ -z "$ASPNET_VERSION" ]; then echo "No .NET $NET_MAJOR runtime installed here (dotnet --list-runtimes)" >&2; exit 1; fi
  echo "Adding the .NET runtime $ASPNET_VERSION for Windows, Linux and macOS"
  for rid in win-x64 linux-x64 osx-arm64 osx-x64; do
    ext=$([ "$rid" = win-x64 ] && echo zip || echo tar.gz)
    url="https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/$ASPNET_VERSION/aspnetcore-runtime-$ASPNET_VERSION-$rid.$ext"
    mkdir -p "$OUT/runtime/$rid"
    curl -fsSL -o "$OUT/runtime/$rid.$ext" "$url"
    if [ "$ext" = zip ]; then unzip -q "$OUT/runtime/$rid.$ext" -d "$OUT/runtime/$rid"; else tar -xzf "$OUT/runtime/$rid.$ext" -C "$OUT/runtime/$rid"; fi
    rm -f "$OUT/runtime/$rid.$ext"
    # The runtime's own licence texts stay with it (ThirdPartyNotices, LICENSE).
  done
else
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
  dotnet publish "$WEB/bridge/HNMO.Web.csproj" -c Release -r "$RUNTIME" --self-contained \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:HnmoServerSource="$SERVER_SOURCE" -p:DebugType=none -p:DebugSymbols=false -p:GenerateDocumentationFile=false \
    -p:AllowedReferenceRelatedFileExtensions=.no-related-files -o "$OUT/web" -v quiet
  find "$OUT/web" -maxdepth 1 \( -name "*.pdb" -o -name "*.xml" \) -delete
fi

# Strip any precompressed .br/.gz copies from server/wwwroot and pack ItemEditor icons into items.zip.
if [ -d "$OUT/server/wwwroot" ]; then
  SERVER_WWWROOT_BEFORE_FILES="$(find "$OUT/server/wwwroot" -type f | wc -l | tr -d ' ')"
  SERVER_WWWROOT_BEFORE_MB="$(du -sm "$OUT/server/wwwroot" | awk '{print $1}')"
  find "$OUT/server/wwwroot" -type f \( -name '*.br' -o -name '*.gz' \) -delete
  ITEM_ICONS_DIR="$OUT/server/wwwroot/_content/HNMO.Web.ItemEditor/img/items"
  if [ -d "$ITEM_ICONS_DIR" ]; then
    echo "Packing ItemEditor icons"
    dotnet run --project "$WEB/bridge/HNMO.Web.csproj" -c Release -p:HnmoServerSource="$SERVER_SOURCE" -- \
      --pack-icons "$ITEM_ICONS_DIR" "$OUT/server/wwwroot/_content/HNMO.Web.ItemEditor/img/items.zip"
    rm -rf "$ITEM_ICONS_DIR"
  fi
  SERVER_WWWROOT_AFTER_FILES="$(find "$OUT/server/wwwroot" -type f | wc -l | tr -d ' ')"
  SERVER_WWWROOT_AFTER_MB="$(du -sm "$OUT/server/wwwroot" | awk '{print $1}')"
fi

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
# The game keeps its function names (no identifier minifying), so the performance report's profile
# (Settings, "Performance report") names the functions that take the time.
"$WEB/node_modules/.bin/esbuild" "$OUT/web/wwwroot/js/main.js" \
  --bundle --minify-whitespace --minify-syntax --format=esm --external:three \
  --outfile="$OUT/web/wwwroot/js/main.min.js" --log-level=warning
# The website (home.html) has its own script: bundled the same way.
"$WEB/node_modules/.bin/esbuild" "$OUT/web/wwwroot/js/website.js" \
  --bundle --minify --format=esm --external:three \
  --outfile="$OUT/web/wwwroot/js/website.min.js" --log-level=warning
"$WEB/node_modules/.bin/esbuild" "$OUT/web/wwwroot/js/topupPage.js" \
  --bundle --minify --format=esm \
  --outfile="$OUT/web/wwwroot/js/topupPage.min.js" --log-level=warning
"$WEB/node_modules/.bin/esbuild" "$OUT/web/wwwroot/js/calculator.js" \
  --bundle --minify --format=esm \
  --outfile="$OUT/web/wwwroot/js/calculator.min.js" --log-level=warning
find "$OUT/web/wwwroot/js" -name '*.js' ! -name 'main.min.js' ! -name 'website.min.js' ! -name 'topupPage.min.js' ! -name 'calculator.min.js' -delete
find "$OUT/web/wwwroot/js" -mindepth 1 -type d -empty -delete
# Point the packaged pages at the bundles (dev pages are untouched).
sed -i 's#src="/js/main.js"#src="/js/main.min.js"#' "$OUT/web/wwwroot/index.html"
sed -i 's#src="/js/website.js"#src="/js/website.min.js"#' "$OUT/web/wwwroot/home.html"
sed -i 's#src="/js/topupPage.js"#src="/js/topupPage.min.js"#' "$OUT/web/wwwroot/topup.html"
sed -i 's#src="/js/calculator.js"#src="/js/calculator.min.js"#' "$OUT/web/wwwroot/calculator.html"
# Every script a page loads must be in the folder (a missing one leaves the page empty).
missing=0
for page in "$OUT/web/wwwroot"/*.html; do
  for src in $(grep -o '<script[^>]*src="/[^"]*"' "$page" | sed 's#.*src="/\([^"]*\)"#\1#'); do
    if [ ! -f "$OUT/web/wwwroot/$src" ] && [ ! -f "$OUT/web/$src" ]; then
      echo "Missing script for $(basename "$page"): /$src" >&2
      missing=1
    fi
  done
done
[ "$missing" = 0 ] || { echo "The packaged pages need scripts that aren't in the folder." >&2; exit 1; }

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
  # Sounds and music as Opus: about 250 MB less (WAV effects shrink ~95 %). The web game asks for
  # the .opus names when Data/Audio/Opus.json is there and the browser plays Opus. Set
  # HNMO_KEEP_WAV=1 to keep the original files.
  if [ "${HNMO_KEEP_WAV:-0}" != 1 ] && command -v ffmpeg >/dev/null; then
    echo "Converting the sounds and music to Opus"
    convert_audio() {
      local file="$1" rate out
      out="${file%.*}.opus"
      case "$file" in */[Mm]usic/*) rate=96k ;; *) rate=48k ;; esac
      if ffmpeg -nostdin -v error -y -i "$file" -c:a libopus -b:a "$rate" -vbr on "$out" && [ -s "$out" ]; then
        rm -f "$file"
      else
        rm -f "$out"; echo "  kept $file (could not convert)" >&2
      fi
    }
    export -f convert_audio
    # The web game's own sounds (the HNMO skills', web/wwwroot/data/Sound) go with them.
    find "$OUT/Data/Sound" "$OUT/Data/Music" "$OUT/web/wwwroot/data/Sound" -type f \( -iname '*.wav' -o -iname '*.mp3' -o -iname '*.ogg' \) -print0 \
      | xargs -0 -P "$(nproc)" -I{} bash -c 'convert_audio "$1"' _ {}
    mkdir -p "$OUT/Data/Audio"
    printf '{ "opus": true }\n' > "$OUT/Data/Audio/Opus.json"
    du -sh "$OUT/Data/Sound" "$OUT/Data/Music"
  elif [ "${HNMO_KEEP_WAV:-0}" != 1 ]; then
    echo "WARNING: ffmpeg not found: the sounds stay WAV / MP3 (about 250 MB more)."
  fi

  DATA_BEFORE_FILES="$(find "$OUT/Data" -type f | wc -l | tr -d ' ')"
  DATA_BEFORE_MB="$(du -sm "$OUT/Data" | awk '{print $1}')"
  ALLOWLIST_FILE="$(mktemp)"
  echo "Building web Data allowlist and packing Data/packs"
  node "$WEB/tools/build-allowlist.mjs" "$OUT/Data" "$ALLOWLIST_FILE"
  if [ "${HNMO_SKIP_KTX2:-0}" != 1 ]; then
    echo "Converting 3D textures to KTX2 (UASTC/ETC1S with mipmaps)"
    node "$WEB/tools/convert-textures-ktx2.mjs" "$OUT/Data" "$ALLOWLIST_FILE"
  fi
  dotnet run --project "$WEB/bridge/HNMO.Web.csproj" -c Release -p:HnmoServerSource="$SERVER_SOURCE" -- \
    --pack "$OUT/Data" "$OUT/Data/packs" --allowlist "$ALLOWLIST_FILE"
  rm -f "$ALLOWLIST_FILE"
  find "$OUT/Data" -mindepth 1 -maxdepth 1 ! -name 'packs' -exec rm -rf {} +
  DATA_AFTER_FILES="$(find "$OUT/Data" -type f | wc -l | tr -d ' ')"
  DATA_AFTER_MB="$(du -sm "$OUT/Data" | awk '{print $1}')"
else
  mkdir -p "$OUT/Data"
fi

# HNMO's own licence is licenses/LICENSE.txt. The parts HNMO is built on keep their licence
# texts word for word (their copyright notices must stay in every copy) in one separate file,
# licenses/THIRD-PARTY-NOTICES.txt.
mkdir -p "$OUT/licenses"
{
  printf 'HNMO\nCopyright %s HNMO. All rights reserved.\n\n' "$(date -u +%Y)"
  printf 'This folder also contains parts made by others, each under its own licence:\n'
  printf 'see THIRD-PARTY-NOTICES.txt next to this file.\n'
} > "$OUT/licenses/LICENSE.txt"
notice() { printf '================================================================================\n%s\n================================================================================\n\n' "$1"; }
{
  printf 'Third-party notices\n\nHNMO includes the following parts, under their own licences.\n\n'
  notice 'Game server and admin panel (MIT License)'
  cat "$SERVER_SOURCE/licenses/LICENSE.txt"; printf '\n\n'
  notice 'three.js (MIT License), the 3D library of the web game'
  cat "$WEB/node_modules/three/LICENSE"; printf '\n\n'
  notice 'DejaVu fonts (Bitstream Vera / DejaVu licence), the text of the web game'
  cat "$WEB/public/fonts/DejaVuSans-LICENSE"; printf '\n\n'
  if [ -n "${POSTGRES_DIR:-}" ]; then
    notice 'PostgreSQL (PostgreSQL License), the save game database'
    printf 'PostgreSQL is released under the PostgreSQL License: https://www.postgresql.org/about/licence/\n'
  fi
} > "$OUT/licenses/THIRD-PARTY-NOTICES.txt"
cp "$WEB/tools/hnmo/README.txt" "$OUT/README.txt"
cp "$WEB/tools/hnmo/start-hnmo.sh" "$WEB/tools/hnmo/Start HNMO.bat" "$OUT/"
# Windows reads batch files reliably only with Windows line endings (CRLF), whatever the checkout has.
sed -i 's/\r*$/\r/' "$OUT/Start HNMO.bat"
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
    for exe in server/HNMO.Startup server/HNMO.Startup.exe server/HNMO.Startup.dll; do
      [ -f "$exe" ] && MANIFEST_FILES+=("$exe")
    done
    while IFS= read -r f; do MANIFEST_FILES+=("$f"); done < <(find server -maxdepth 1 -name 'HNMO.*.dll' | sort)
    for launcher in web/HNMO web/HNMO.exe web/HNMO.dll; do
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

# Guard: no source must ever leak into a shared folder. Fail hard if it did.
echo "Checking that no source leaked into the folder"
LEAKED="$(find "$OUT" \( -name '*.ts' -o -name '*.cs' -o -name '*.csproj' -o -name '.git' \) -print)"
if [ -n "$LEAKED" ]; then
  echo "ERROR: source files leaked into the package — refusing to ship:" >&2
  echo "$LEAKED" >&2
  exit 1
fi

echo ""
echo "Package summary ($OUT):"
printf '  %-16s %10s %10s\n' "Folder" "Files" "MB"
for dir in Data server web postgres licenses; do
  if [ -d "$OUT/$dir" ]; then
    fcount="$(find "$OUT/$dir" -type f | wc -l | tr -d ' ')"
    mb="$(du -sm "$OUT/$dir" | awk '{print $1}')"
    printf '  %-16s %10s %10s\n' "$dir" "$fcount" "${mb} MB"
  fi
done
if [ -n "${DATA_BEFORE_FILES:-}" ]; then
  printf '  Data before pack: %s files, %s MB -> after pack: %s files, %s MB\n' \
    "$DATA_BEFORE_FILES" "$DATA_BEFORE_MB" "$DATA_AFTER_FILES" "$DATA_AFTER_MB"
fi
if [ -n "${SERVER_WWWROOT_BEFORE_FILES:-}" ]; then
  printf '  server/wwwroot before: %s files, %s MB -> after: %s files, %s MB\n' \
    "$SERVER_WWWROOT_BEFORE_FILES" "$SERVER_WWWROOT_BEFORE_MB" "$SERVER_WWWROOT_AFTER_FILES" "$SERVER_WWWROOT_AFTER_MB"
fi
TOTAL_FILES="$(find "$OUT" -type f | wc -l | tr -d ' ')"
TOTAL_MB="$(du -sm "$OUT" | awk '{print $1}')"
printf '  %-16s %10s %10s\n' "TOTAL" "$TOTAL_FILES" "${TOTAL_MB} MB"
echo "Done: $OUT"
