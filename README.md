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


Download [HNMO-windows.zip](https://github.com/nguyentuanphu/HNMO/releases/latest/download/HNMO-windows.zip)
(always the newest build), unzip it and double-click `Start HNMO.bat`. The game opens in the browser; your
progress is saved in the folder's `save` directory. The folder's `README.txt`
explains accounts, coins, the admin panel and playing with friends (Tailscale).

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
