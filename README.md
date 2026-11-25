# HNMO

A 3D online role-playing game you play in the web browser, alone or together
with friends. Everything is in this repository: the game server, the admin
panel, the web client and the game data.

| Folder | What's in it |
|---|---|
| [`server/`](server) | The game server: login and connect servers, game servers, chat, guilds, the admin panel and the API (.NET 10, PostgreSQL) |
| [`client/web/`](client/web) | The web game: the browser client (three.js), the HNMO launcher that starts everything with one click, the account page with free coins |
| [`client/src/bin/Data`](client/src/bin/Data) | The game data: maps, models, textures |
| [`client/`](client) | The desktop client and its tools |

## Play

Sending the game to a friend? Give them [`friends/README.md`](friends/README.md):
it has the download link and both ways to play.


| System | Start the server |
| --- | --- |
| Windows | Extract [HNMO.zip](https://github.com/nguyentuanphu/HNMO/releases/latest/download/HNMO.zip) and open `Start HNMO.bat`. |
| macOS (Apple Silicon and Intel) | Extract [HNMO-macos.zip](https://github.com/nguyentuanphu/HNMO/releases/latest/download/HNMO-macos.zip) and open `Start HNMO.app`. |
| Ubuntu and Debian (x64 and ARM64) | Open [HNMO-linux.deb](https://github.com/nguyentuanphu/HNMO/releases/latest/download/HNMO-linux.deb) in the software installer, then open **HNMO** from the applications menu. |

The server opens the game in your browser. The downloads include .NET and
PostgreSQL. See the packaged `README.txt` for first launch and save locations.

## Build

You need the .NET 10 SDK and Node.js 22.

```bash
npm ci --prefix client/web
# The whole HNMO folder (server, web client, data); add POSTGRES_DIR=<portable PostgreSQL> to bundle the database
client/web/tools/package-hnmo.sh "$PWD/server" win-x64 "$PWD/dist/HNMO"   # or linux-x64
```

Server only:

```bash
dotnet build server/src/HNMO.sln -c Release
dotnet test server/src/HNMO.sln -c Release --no-build
```

Web client during development: see [`client/web/README.md`](client/web/README.md).

## Licenses

The server is released under the MIT license ([`server/licenses/LICENSE.txt`](server/licenses/LICENSE.txt)).
