# Season 9 as its own program: check in a packaged folder (2026-11-20, Sydney)

Job 8 of `docs/prompts/local-claude-overnight-2026-11-19.md`. Branch `seasons-admin` at `1de40d1b` (#98, which
contains #97 `seasons-databases`), packaged on the owner's Windows PC:
`HNMO_S9_DATA_DIR=<scratch>/s9-pack client/web/tools/package-hnmo.sh "$PWD/server" portable "<scratch>/HNMO-pkg"`
(13,391 files, 2,122 MB). `portable` because the PC's application control blocks the unsigned `HNMO.exe`.

## 1. Switching Season 9 on

Admin panel (8095) → Host settings → "Season 9" ticked → Save settings: "Host settings saved." Restarted HNMO (the
launcher `dotnet HNMO.dll`, its output kept). The launcher's log:

```
Season 9 is starting (connect server 44506, admin panel http://127.0.0.1:8096). Its first start sets up its save and takes a few minutes.
```

Listening afterwards: 8080, 8095, **8096**, 44406, **44506**, 55432. `/data/seasons.json`:
`{"s6":true,"s9":true,"connect":{"s6":44406,"s9":44506}}` (before: `"s9":null`).

## 2. The start screen and playing Season 9

| check | result |
|---|---|
| Start screen columns | two: "Season 6.3 / The classic worlds" (3 servers: `World 1 — Origin — Server 0..2`) and "Season 9 / The newer worlds, more maps" (3 servers: `Server 1..3`) - `docs/screenshots/local-s9-program-2026-11-20/start-screen.png` |
| A Season 9 server with a Season 6.3 account | **works**: test5 logged in on the start screen, picked the first Season 9 server; the browser's relay went to the connect server 44506, then game server 46002; the character list showed test5's four characters (level 51); test5Dk entered Lorencia (133,121), no page errors - `season9-lorencia.png` |
| "Play" from the website for a Season 9 server | **not checked**: the website answers "The host needs to finish the website connection setup." A fresh folder has no website key (`save/api-key.txt`); the admin panel creates a key only after its first admin user exists. I created a test admin user (`hnmotest`) in this throwaway folder, but my scripted sign-in on `/login` didn't sign in (the form posted, the page stayed, the user's failed-login count stayed 0), so I deleted the user again to leave the folder as it was. |
| Coins bought on the website show in Season 9 | **the sharing works; the website purchase not checked** (same setup missing). In its place: +500 WCoinC on test5 in the shared database (`hnmo`); after test5 logged in on a Season 9 server again, Season 9's database (`hnmo_s9`) had test5 at 500 WCoinC, 20 Goblin Points (before: 0 and 20 in both). |

Seen on the way:
- The Season 9 column's servers have no world name (`Server 1`), the Season 6.3 ones do (`World 1 — Origin — Server 0`).
- On `/login` (8095) the panel's "An unhandled error has occurred. Reload" bar shows from the first load
  (`#blazor-error-ui` `display: block`), with no error in the browser console.

## 3. Import server in Season 9's admin panel (8096)

The local Season 9 server files (one archive of the game folder, unpacked into `<scratch>/s9-server`: 508 files; the
importer reads its config folders). `/import-server` on 8096, folder set to it, the defaults (rates, drop lists, NPC
shops, event schedules, chaos mix rates, monster spawns; not the database entities: no SQL database here), **Run
Dry-Run Preview**:

```
An item with the same key has already been added. Key: 9
   at System.Linq.Enumerable.ToDictionary(...)
   at HNMO.GameLogic.ServerImporter.LegacyServerImportEngine.BuildDryRunPreviewAsync(...)
   at HNMO.Web.AdminPanel.Pages.ImportServer.RunDryRunAsync()
```

**Cause: not the Season 9 files, the importer.** `LegacyServerImportEngine.cs` line 77 (and line 368, the apply step)
builds `config.Maps.ToDictionary(m => (int)m.Number)`. HNMO's game configuration has, by design, several maps with
the same number: Devil Square 1-4 are all map 9 and Devil Square 5-7 all map 32 (checked in `config.GameMapDefinition`
of both `hnmo` and `hnmo_s9`). So the preview throws on every HNMO configuration, Season 6.3 and Season 9 alike.
The monster lookups next to it already use `GroupBy(m => (int)m.Number).ToDictionary(g => g.Key, g => g.First())`;
doing the same for the maps (lines 77 and 368) should let the preview run. Not changed here (the importer is in the
seasons work); no preview counts or warnings could be read, and nothing was imported.

## Not done

- Website "Play" for a Season 9 server and a real website purchase (the folder's website setup needs an admin
  user and an API key; my sign-in didn't go through).
- The import (blocked by the preview's exception above).
