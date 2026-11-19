# Local Season 9 check, 2026-11-21 (Sydney time)

Run on the owner's Windows PC, branch `local-s9-check` (from `claude/hnmo-main-handover-54co2m` at `cbac221b`).
Folders are written as `<game folder>`, `<Season 9 Data>` (the unpacked Season 9 `Data` folder,
18,950 files, 1,217 MB) and `<scratch>` (a work folder outside the repo). The game folder itself holds only
archives; nothing in it or in `<Season 9 Data>` was changed or run.

## Task 1: why some Season 9 textures are missing

Searched both data folders for each name, ignoring case and extension (`find . -iname "<name>.*"`), then read
the texture names inside the Season 9 models with the web client's own BMD reader (`parseBMD`, which
decrypts the Season 9 model format):

```
Object1/Bridge01.bmd: bridge_shadow01.tga        Object1/Light01..03.bmd, PoseBox01.bmd: ston03.jpg
Object3/Object102.bmd: sword14.jpg, sword14.tga, sword11.jpg, sword11.tga, sword06.jpg, hound_sword1.jpg,
                       hound_shield1.tga, shield05.jpg, sword03.jpg
Object3/Object105.bmd: sword03.jpg
Object4/Object02.bmd, Object10.bmd: filling_up02.jpg            Object4/Object40.bmd: itp01.tga
```

| requested | found in Season 9 at | found in base data at | notes |
|---|---|---|---|
| `Object1/ston03` | `Object1/ston03.OZJ` | `Object1/ston03.OZJ` (same bytes) | **not reproduced**: served in this check (`/data/s9/Object1/ston03.jpg` → 200, 4,516 bytes; `bridge_shadow01.tga` → 200, 300 bytes) from both roots. The model asks with the same name in the same folder. The earlier failure did not come from the data or the name mapping. |
| `Object1/bridge_shadow01` | `Object1/bridge_shadow01.OZT` | `Object1/bridge_shadow01.OZT` | same as above |
| `Effect/twlighthik01` | `Skill/twlighthik01.OZJ` | `Skill/twlighthik01.OZJ` | **in another folder**: the web game asks `Effect` first, then `Skill` (`render/effects/core.ts`, `loadTexture(['Effect','Skill'], …)`), so the `Effect` miss is expected and the `Skill` request answers it. |
| `Object3/sword14` | `Item/sword14.OZJ`, `Item/sword14.ozt`, `Item/Texture/sword14.*` | `Item/sword14.OZJ`, `Item/sword14.ozt` | **in another folder**: new Season 9 Devias model `Object3/Object102.bmd` (a weapon display) uses item textures, which live only in `Item/`. The web game looks only in the model's own folder. |
| `Object3/sword11` | `Item/sword11.OZJ`, `.ozt`, `Item/Texture/` | `Item/sword11.OZJ`, `.ozt` | in another folder (same model) |
| `Object3/hound_sword1` | `Item/`, `Item/Texture/`, `NPC/hound_sword1.OZJ` | `Item/`, `NPC/` | in another folder (same model) |
| `Object3/hound_shield1` | `Item/hound_shield1.ozt`, `Item/Texture/` | `Item/hound_shield1.ozt` | in another folder (same model) |
| `Object3/sword06` | `Item/`, `Item/Texture/`, `NPC/sword06.OZJ` | `Item/`, `NPC/` | in another folder (same model) |
| `Object3/shield05` | `Item/shield05.OZJ`, `Item/Texture/` | `Item/shield05.OZJ` | in another folder (same model) |
| `Object3/sword03` | `Item/`, `Item/Texture/`, `NPC/sword03.OZJ` | `Item/`, `NPC/` | in another folder (`Object102.bmd` and `Object105.bmd`) |
| `World4/TileGrass02` | not in `World4` (only `TileGrass01`, `TileGrass03`); in 40+ other `WorldN` | not in `World4` either | **missing**: Noria has no second grass tile in either season. `terrain.ts` asks for a fixed list of tile names, so this request is expected to fail for both seasons. |
| `Object4/filling_up02` | `Object4/filling_up02.OZJ` | `Object4/filling_up02.OZJ` | **not reproduced**: `/data/s9/Object4/filling_up02.jpg` → 200, 4,767 bytes, from both roots. |
| `Object4/itp01` | `Object4/itp01.OZT` (also `Object31/`) | `Object4/itp01.OZT` | **not shipped** (a fourth kind): the file is in both folders with the same bytes, but only the new Season 9 model `Object4/Object40.bmd` uses it. The base packs keep only files a base model or list uses (`tools/build-allowlist.mjs`), so the HNMO folder drops it, and the pack builder dropped it too as "same as base". Fixed in Task 2: now 200, 2,092 bytes from `/data/s9/`; still 404 from `/data/` (correct, Season 6 never asks). |

How the web game asks (`client/web/bridge/GameFiles.cs`, `client/web/src/bmd.ts`): a texture is requested
under the model's folder with the name inside the model (`/data/<dir>/<name>.jpg|.tga`). In compressed
mode it first asks `.ktx2` (the name with its extension swapped), then the name itself. The server matches
names ignoring case and, when `x.jpg` / `x.jpeg` / `x.tga` / `x.png` is not there, answers with
`x.OZJ` / `x.OZJ` / `x.OZT` / `x.OZP`, unwrapped. `/data/s9/<path>` asks the Season 9 folder first (only for
`WorldN`, `ObjectN`, `Monster`, `Item`, `Skill`, `Effect`, `Player`, `NPC`, `Sound`, `Music`), then the base game.
There are no `.ktx2` files in the Season 9 data, so every `.ktx2` request for a Season 9 file fails; that
is expected and falls through to the `.jpg` / `.tga`.

Note: `Item/Texture/` is a new Season 9 folder with lower-case `.ozj` / `.ozt` copies of item textures. Nothing
in the web game asks for it.

Summary of the 13 names:
* **missing** (in neither folder): `World4/TileGrass02`. Expected; both seasons fail it the same way.
* **in another folder**: `Effect/twlighthik01` (answered by `Skill/`, so harmless) and the 7 Devias item textures. They really don't show: `Object3/Object102.bmd` and `Object105.bmd` draw untextured (grey) parts. Making them show would mean looking in `Item/` too for world objects, but first check what the desktop client does with these two models.
* **not asked for** (exists, but the web game asks a name or extension it doesn't map): none.
* **not shipped**: `Object4/itp01`, plus 2,712 more Season 9 files of the same kind (see Task 2). Fixed.
* **not reproduced**: `Object1/ston03`, `Object1/bridge_shadow01`, `Object4/filling_up02`: served fine here.

## Task 2: build the Season 9 pack

All runs went through the PC's heavy-job queue (one heavy job at a time, low priority), written here as plain commands.

```
node client/web/tools/build-s9-pack.mjs "<Season 9 Data>" "client/src/bin/Data" "<scratch>/s9-pack" --dry-run
Folder        taken      size   same as base
Effect           76     3.2 MB      323
Item           2132    69.0 MB     1272
Monster         314    37.4 MB      947
Music             9    23.7 MB       41
NPC              43    10.5 MB      335
ObjectN        4135   140.6 MB     3337
Player            2     3.4 MB     1028
Skill            37     2.0 MB      366
Sound           130    61.9 MB      883
WorldN          412    38.8 MB      847
Total          7290   390.4 MB     9379
Left out (never served from a pack): 2296 files in (top level), Facebook, InGameShopBanner, InGameShopScript, Interface, Local, Logo
```

The real run printed the same table and `Pack written to <scratch>/s9-pack`. The script did not crash, but
three bugs came out, all fixed in `client/web/tools/build-s9-pack.mjs` with tests:

1. **Windows folder notes were copied.** 3 of the 7,290 files were `Object2/Thumbs.db`, `World2/Thumbs.db` and
   `World2/descript.ion`. `Thumbs.db`, `desktop.ini` and `descript.ion` are now skipped (`FOLDER_NOTES`).
2. **The unit test failed on Windows** (`node --test …` → 1 of 3 failed: `monster/Monster300.bmd` instead of
   `Monster/Monster300.bmd`). Its made-up season tree had both `monster/` and `Monster/`, which are one folder on
   a Windows disk. The different-case file is now in a folder of its own (`item/`).
3. **Files that are the base game's but not in the HNMO folder were dropped** (found in Task 3). A packaged
   folder keeps only the base files on `build-allowlist.mjs`'s list, so a file the season shares with the
   base game but only a Season 9 model uses (`Object4/itp01.OZT`, new Devias models `Object3/Object113.bmd`…,
   Noria's `World4/TileGrass01.OZT`) was in neither the pack nor the HNMO folder: 404. New option
   `--base-allowlist <file>`: a file the same as the base game's is left out only when the allowlist names it.

Final run, with the allowlist:

```
node client/web/tools/build-allowlist.mjs client/src/bin/Data "<scratch>/base-allowlist.txt"
Allowlist: 10453/14891 files, 737.90/1029.01 MB
node client/web/tools/build-s9-pack.mjs "<Season 9 Data>" "client/src/bin/Data" "<scratch>/s9-pack" --base-allowlist "<scratch>/base-allowlist.txt"
Folder        taken      size   same as base
Effect           76     3.2 MB      323
Item           2132    69.0 MB     1272
Monster         437    42.0 MB      824
Music             9    23.7 MB       41
NPC              91    12.1 MB      287
ObjectN        5758   229.0 MB     1712
Player            2     3.4 MB     1021
Skill            37     2.0 MB      366
Sound           637   141.1 MB      376
WorldN          818    66.8 MB      438
Total          9997   592.1 MB     6660
Taken although the same as base (the base allowlist leaves them out): 2710 files
Left out (never served from a pack): 2293 files in (top level), Facebook, InGameShopBanner, InGameShopScript, Interface, Local, Logo
Pack written to <scratch>/s9-pack.

node --test client/web/tests/build-s9-pack.test.mjs
ℹ tests 4
ℹ pass 4
ℹ fail 0
```

Before the fixes the same test command ended `ℹ tests 3 … ℹ fail 1`. Of the 2,710 extra files, 508 are sounds
and 1,624 are `ObjectN`. Not all of them are used by Season 9 maps, but the pack can't tell which are, and
592 MB is still half of the full folder. (`Player/player.bmd` stays: it differs from the base game's.)

Suggestion, not done (outside this task's allowed changes): `package-hnmo.sh` could pass the allowlist it
builds to the pack builder itself, so the pack and the HNMO folder can't drift apart.

## Task 3: package with the pack and check it

```
export HNMO_S9_DATA_DIR="<scratch>/s9-pack"
export POSTGRES_DIR=<a portable PostgreSQL>     # the PC has none installed
client/web/tools/package-hnmo.sh "$PWD/server" win-x64 "<scratch>/HNMO-pkg-s9"
…
Package summary (<scratch>/HNMO-pkg-s9):
  Data                     50     794 MB
  server                   69     224 MB
  web                      93     133 MB
  postgres               1631     124 MB
Done: <scratch>/HNMO-pkg-s9
```

Getting there on this PC took three things, none in the repo:
* The .NET 10 SDK is installed per user (`%LOCALAPPDATA%\Microsoft\dotnet`), not on PATH; added to PATH.
* `-r win-x64` builds the persistence source generator into `SourceGenerator/bin/Release/win-x64/`, but
  `HNMO.Persistence.csproj`'s PreBuild step runs it with `dotnet run --no-build` from `bin/Release/`, so the
  first run failed with `MSB3073` ("cannot find …SourceGenerator.exe"). One `dotnet build` of the generator
  without a runtime put it there. Worth fixing in the csproj (or building it first in `package-hnmo.sh`).
* `npm ci --prefix client/web` before packaging (else `tsc` is missing).

`Data_S9` in that folder: **408 MB** on disk (7,287 files, 390.3 MB of data), against 1,217 MB with the whole
Season 9 folder. With the Task 2 fix (the pack I'd ship): **592.1 MB**, 9,997 files.

**`Start HNMO.bat` did not start the win-x64 folder:** Windows' Application Control policy blocks the newly
built, unsigned `web\HNMO.exe` ("An Application Control policy has blocked this file"). I didn't change that
setting. I built the same thing as `portable` (`… portable "<scratch>/HNMO-pkg-s9p"`), which starts with the
signed `runtime\win-x64\dotnet.exe HNMO.dll`; that started (PostgreSQL 55432, web 8080, admin 8095). After the
Task 2 fix I put the new pack into its `Data_S9`; without ffmpeg packaging only copies the pack there, so that
matches a rebuild. While it restarted, another HNMO folder on this PC took ports 8080/8095. I didn't stop it, and
checked my folder's web server alone on a spare port (`--urls http://127.0.0.1:8181 --Launcher:Enabled false`).

Checks (curl, last run on the final pack):

| check | result |
|---|---|
| `/data/seasons.json` | `{"s6":true,"s9":true}` |
| `/data/Player/player.bmd` vs `/data/s9/Player/player.bmd` | 3,324,834 vs 3,472,315 bytes: differ |
| `/data/s9/Local/…` = `/data/Local/…` | 4 files (`Local/Eng/JewelOfHarmonyOption_eng.bmd`, `MasterSkillTooltip_eng.bmd`, `Minimap/Minimap_World1_eng.bmd`, `Minimap_World11_eng.bmd`): same sha256 |
| `/data/s9/Interface/…` = `/data/Interface/…` | 4 files (`BalgasBarrack.OZT`, `Menu01.OZJ`, `atlans.OZT`, `hourglass.OZT`): same sha256 |

Task 1's names against the final pack (`s9` = `/data/s9/…`, `base` = `/data/…`):

```
Object1/ston03.jpg           s9=200 4516   base=200 4516
Object1/bridge_shadow01.tga  s9=200 300    base=200 300
Effect/twlighthik01.jpg      s9=404        base=404      (then Skill/twlighthik01.jpg 200 31530)
Object3/sword14.jpg          s9=404        base=404      (Item/sword14.jpg 200 9312)
Object3/hound_shield1.tga    s9=404        base=404
World4/TileGrass02.jpg       s9=404        base=404
Object4/filling_up02.jpg     s9=200 4767   base=200 4767
Object4/itp01.tga            s9=200 2092   base=404      (404 in both before the Task 2 fix)
World4/TileGrass01.tga       s9=200 65580  base=404      (404 in both before the fix: new)
Object3/Object113.bmd        s9=200 29932  base=404      (404 in both before the fix: new)
Object1/ston03.ktx2          s9=404        base=404      (no KTX2 in the pack: expected)
```

### Walk in a Season 9 world

Once the owner closed the other folder, `Start HNMO.bat` in `<scratch>/HNMO-pkg-s9p` started everything. The
folder's only world (`main`) was switched to Season 9 in the admin panel (Worlds → Edit → Season 9 → Save:
"Saved world settings."). It is a throwaway test folder, so no new world or database was made, and
`/website/data` then gave `main s9`. A Playwright script (headless Chromium on the GPU,
`--use-angle=d3d11`, final pack) logged in as `testgm` and:
* clicked a loop of six ground points in Lorencia (`lorencia.png`);
* went `/move devias` and walked the same loop (`devias.png`, 60 fps);
* went `/move noria` too, since Task 1's list has Noria names (`noria.png`).

```
entered {"map":0,...}
requests 1349 console 192
statuses {"200":1187,"206":3,"404":79,"failed:net::ERR_ABORTED":80} unique 1185
```

Grouping the `/data/s9/` requests by path without extension gives 36 paths with no 200 in any form. After
checking each:

| paths | map | why | new vs Task 1? |
|---|---|---|---|
| `Music/main_theme.mp3`, `Music/devias.mp3`, `Music/noria.mp3` | all | 206 Partial Content (the audio element streams with a range). They are **not failures**. | – |
| 24 `Skill/…` effect textures (`blur02`, `chrome01`, `damage1`, `flare`, `shockwave`, `volcano_stone`, …) | Lorencia | The effect loader asks `Effect` and `Skill`; every one of these got 200 from `Effect/` (same as `twlighthik01`, which got 200 from `Skill/`). **Expected misses.** | new names, same kind as `Effect/twlighthik01` |
| `Effect/twlighthik01` | Lorencia | answered by `Skill/` (200) | in Task 1 |
| `Object3/sword14`, `sword11`, `sword06`, `sword03`, `shield05`, `hound_sword1`, `hound_shield1` | Devias | item textures of `Object3/Object102.bmd` / `Object105.bmd`, only in `Item/`. **Real**: the console says `Texture missing: sword14.jpg in Object3` etc. (9 warnings). | in Task 1 |
| `World4/TileGrass02` | Noria | in neither season. **Real but expected.** | in Task 1 |

The 80 `net::ERR_ABORTED` are the browser dropping the rest of a 404 response or a range request; each one has
a 404 or 206 for the same path. `Object4/itp01`, `World4/TileGrass01` and the other "not shipped" files gave
200 (the Task 2 fix). **Nothing new fails**: every real failure is one of Task 1's names.

Console: no page errors. Apart from the 9 `Texture missing` warnings above, there were only the browser's
"Failed to load resource: 404" lines for the misses in the table, two `Permissions-Policy` header warnings
(`join-ad-interest-group`, `run-ad-auction`) and "Service Worker registration blocked by Playwright" (on
purpose).

Screenshots: `docs/screenshots/local-s9-2026-11-21/lorencia.png`, `devias.png`, `noria.png`.
