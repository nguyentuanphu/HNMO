# HNMO: instructions for Claude

Read this first in every session. It's what the owner expects; keep it up to date
when you learn something new about how they work (see the `hnmo` skill).

## Rules

- **Name:** the game, its parts and everything visible are called **HNMO**. In
  commits, pull requests, docs, comments and code, never mention the original
  game, its publisher or the projects this one started from. Keep third-party
  copyright notices and license files as they are (they're legally required).
- **Time:** read and report all times in Sydney time (Australia/Sydney).
- **Small steps:** build features one at a time, test each in the real game
  (headless Chromium + the packaged HNMO folder), then commit.
- **Report:** say what was verified and what wasn't; give download links for builds.

## Layout

- `server/` — game server + admin panel (.NET 10). Solution `server/src/HNMO.sln`,
  start project `server/src/Startup/HNMO.Startup.csproj`. Database name `hnmo`.
- `client/web/` — the web game. `public/` (browser client, ES modules, three.js),
  `bridge/` (HNMO.Web: launcher, `/relay` WebSocket→TCP, `/data`, `/portal`),
  `tools/package-hnmo.sh` (builds the HNMO folder), `PARITY.md` (feature checklist).
- `client/src/bin/Data` — game data the web client reads.
- `client/` — desktop client (C++/CMake) and `ClientLibrary` (.NET). They take the
  packet definitions from `server/src/Network/Packets` (`HnmoServerSource`).

## Commands

```bash
dotnet build server/src/HNMO.sln -c Release && dotnet test server/src/HNMO.sln -c Release --no-build
npm ci --prefix client/web
node client/web/tools/generate-packets.mjs server/src/Network/Packets   # after packet XML changes
client/web/tools/package-hnmo.sh "$PWD/server" linux-x64 /tmp/HNMO     # whole folder
```

The packaged folder starts with one launch: `start-hnmo.sh` / `Start HNMO.bat`
start PostgreSQL (port 55432, data in `save/`), the server (admin panel on
127.0.0.1:8095, connect server 44406) and the web game (port 8080).
Test accounts: test0…test9, test300, test400 (password = name).
PostgreSQL refuses to run as root: test as a normal user.
