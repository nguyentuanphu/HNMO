# HNMO web client (experimental)

A browser version of the client that reads the game data in `src/bin/Data`
directly and plays on the HNMO server. It is being built up to match the desktop
client one to one; [PARITY.md](PARITY.md) tracks what works and what's next.

## What it does

- Logs in to the HNMO server: server list, login, character list, entering the world.
- Draws the map the server puts you on: terrain, light map and all static objects.
- Shows the players, NPCs and monsters in view, with their models and animations
  from `Data/Monsters`, and keeps them in sync as they come, go and walk.
- Click to walk: the path avoids blocked cells and is sent to the server, so other
  players see you walk. Right-drag turns the camera, the mouse wheel zooms.
- "Explore offline" walks Lorencia without a server.

## Run it

### The HNMO folder (players)

`tools/package-hnmo.sh` builds one folder with the game server, its admin panel, the
web client and the game data; nothing has to be installed where it runs (.NET is
included). Start it with `Start HNMO.bat` (Windows) or `start-hnmo.sh`. Its
`README.txt` explains how friends join.

```bash
web/tools/package-hnmo.sh <path to the server repository> win-x64
```

The GitHub workflow "HNMO package" builds the Windows folder as a download.

### From source (developers)

You need the .NET 10 SDK, Node.js 22 and a running server,
e.g. the server program started with `-demo -autostart -resolveIP:loopback`.

```bash
cd client/web
npm install
npm start        # builds the TypeScript, then HNMO.Web: the client, the game data and the relay on port 8081
npm run watch    # in a second terminal: rebuilds on every change
```

The client is TypeScript (`src/`, strict), compiled by `tsc` into `public/js` (not in git).
`npm run typecheck` checks it without writing files; the HNMO folder build and the
"Web client" workflow stop on any type error.

Open <http://127.0.0.1:8081/> and log in, e.g. with the test account `test300` /
`test300`.

`HNMO.Web` (`bridge/`) serves the page, the game data (`/data`) and the relay
(`/relay`) from one address. The relay only connects to the hosts in
`bridge/appsettings.json` (`Relay:AllowedHosts`, plus loopback addresses), so it can't
be used to reach anything else. Don't expose it to the internet: the game data is not
ours to publish.

## Packets

`src/net/packets.generated.ts` is generated from the server's packet
definitions, so every packet the server knows can be read and written by name:

```bash
node tools/generate-packets.mjs <the HNMO server>/src/Network/Packets
```

## How the files are read

The parsers in `src/formats.ts` are ports of the client's loaders, and each
names the C++ function it follows:

| File | Format |
|---|---|
| `EncTerrainN.map` | Texture layers 1 and 2 and the blend alpha per cell, encrypted with `MapFileDecrypt` |
| `EncTerrainN.att` | Walkability flags per cell, `MapFileDecrypt` + `BuxConvert` |
| `EncTerrainN.obj` | Static objects: type, position, angles, scale |
| `TerrainHeight.OZB` | 8-bit BMP after a 4-byte prefix, 1.5 units per level |
| `TerrainLight.OZJ` | Baked light map (JPEG after a 24-byte prefix) |
| `*.bmd` | Models, versions 10 (plain) and 12 (`MapFileDecrypt`); version 14 is not supported yet |
| `*.OZJ` / `*.OZT` | JPEG / TGA with a prefix; the server unwraps them |

## Not done yet

See [PARITY.md](PARITY.md). Next up: attacking monsters (damage, health bars,
dying), then equipment on characters and the HUD.

## Bilingual website

`Start HNMO` opens the website at `/` (also `/home.html`); while it is open, the browser loads the
game's code and first data files in the background, so **Enter the game** (`/index.html`) starts
quickly. English and Vietnamese are switched in the header and remembered by the browser.

**What it shows, all read from the server** (nothing is estimated or made up):

| Section | Source | Refresh |
|---|---|---|
| Players online / capacity, EXP and master EXP rates, level, party and character limits, classes, maps, event rules | `/api/website` → `/website/data` | 30 s |
| Events starting in the next 24 hours (from the event timetables and the server's time zone), shown in the visitor's time zone | `/api/website/details` → `/website/details` | 60 s |
| Character ranking (resets, master level, level; bots and game masters left out), event rankings (saved scores) | same | 60 s (cached 1 min on the server) |
| Drop groups (chance per kill, monster level range, monster, number of items) | same | 60 s |

When the server can't be reached, the numbers and tables are cleared and the page says so; old
values are never shown as current. Each part of the details is independent: one that fails comes
back as "unavailable" (and is logged on the server) while the others still show.

**Accounts.** The Account section registers through the existing portal (`/portal/register`, the
host's API key stays on the server). **Sign in with Google** appears when the host sets a Google
client id (admin panel → Plugins → *Website (admin address, Google sign-in)*; a Google Cloud OAuth
client of type "Web application" with the website's address as an authorized JavaScript origin).
The web server checks Google's token with Google (issuer, this client id, a verified e-mail, not
expired), then finds the game account with that e-mail (the player may set a new game password)
or creates one with the name and password the player chooses. The game itself still logs in with
the account name and password.

**Admin panel through the website.** `https://your-site/adminpanel` opens the admin panel's login.
Change the address in the same plug-in (e.g. to something hard to guess); the website follows
within 30 seconds and the old address stops working. The admin panel always asks for an admin
login there, and stays closed there until an admin user exists (create it on the host computer at
http://127.0.0.1:8095 first). The address is never sent to visitors. Empty turns it off.

**Translations.** Interface texts are in `src/website.ts` (English and Vietnamese). Game content
uses the server's translations: event names come from `PlugInResources.vi.resx` (added for the
events); map, class, drop-group and monster names use the `vi=` part of the server's names when
there is one. Still English on the server (no Vietnamese yet): map, class, drop-group and monster
names, and the Doppelganger and Imperial Guardian events.

The hero uses the existing six `Interface/lo_back_im*.OZJ` game artwork tiles, assembled as
`public/images/realm.jpg`; no artwork from any other site is used. The Windows download points
to this repository's latest release.

Tests: `tests/e2e/website.spec.ts` and `website-details.spec.ts` use fixtures (routed responses);
`tests/e2e/website-live.spec.ts` checks a real server (`LIVE_SERVER=1 BASE_URL=…`).


### Extending the player account page

The account dashboard owns authentication and navigation. Feature modules import
`registerAccountSection` from `src/accountSections.ts` and supply a unique stable id,
English/Vietnamese labels and an `HTMLElement`. The optional async `open(context)`
receives the current language and `context.request<T>('/portal/your-feature', body)`.
This helper adds the current player's credentials in memory and permits only same-origin
portal paths; the server endpoint must still authenticate and authorize every request.
Render player/configuration strings through `textContent`, never HTML. Keep opening a
section read-only; use explicit buttons for claims/purchases. A registration returns a
removal callback; the feature owns disposing its element and listeners. Navigation updates
when sections are registered or removed. No edits to the account page's navigation are
needed. Load the feature module from the account page or its bootstrap module.

The dashboard deliberately leaves `#accountPlaySlot` for the login-ticket feature.
It currently contains only the normal game login link. Do not duplicate ticket creation.
New dashboard texts live in `src/accountPageTexts.ts` as paired English/Vietnamese texts;
this avoids rewriting translation dictionaries being changed on other branches.

Players can change their password in **Security** after leaving the game. Enter the
new password twice (3–20 printable ASCII characters, matching account registration).
Saving signs the player out of the account page; sign in again with the new password.
The bridge keeps the operator API key on the server. The account endpoint verifies
the current password, reserves the offline account against game logins, verifies the
password again, and saves a BCrypt hash. Neither passwords nor hashes are logged or
returned. The page stores only a success notice across the reload, never credentials.

Người chơi có thể đổi mật khẩu trong **Bảo mật** sau khi thoát game. Nhập mật khẩu mới
hai lần, gồm 3–20 ký tự thông thường trên bàn phím tiếng Anh, không có dấu. Sau khi lưu,
trang tài khoản đăng xuất; hãy đăng nhập lại bằng mật khẩu mới. Máy chủ xác thực mật
khẩu hiện tại và giữ chỗ tài khoản để tránh đăng nhập game trong lúc đổi. Trang không
lưu mật khẩu vào bộ nhớ trình duyệt.
