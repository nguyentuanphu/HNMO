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

The Overview mounts the existing login-ticket feature's `#play` section and
`#playNowButton`. Signing in to the dashboard also signs that existing form in;
`#accountPlaySlot` links to it. Ticket creation and fragment removal remain owned
by the existing feature. Signing out there clears the whole account tab.
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

The character table checks current presence through the account's game server when
the player refreshes. A live result must match both the authenticated account and
the character's persistent identifier. Queries share a five-second deadline. A missing
or failed server channel shows **Unavailable**, while saved account data remains usable.
Individual characters' last-seen timestamps are not recorded yet.

Bảng nhân vật kiểm tra trạng thái hiện tại khi người chơi bấm **Làm mới**. Nếu máy chủ
game không trả lời, trang hiển thị **Chưa kiểm tra được**; dữ liệu tài khoản đã lưu vẫn
xem được. Chưa có lịch sử lần xuất hiện gần nhất của từng nhân vật.

The account branch includes `antigravity-integration` (PR #51). Coins mounts its
existing top-up milestone widget; Community mounts its event-alert subscription widget.

Personal missions, streak rewards, the login calendar and referral progress use the
same mission engine as the game. The bridge forwards signed account requests to the
website API, which authenticates the account and stamps its owned character and world
identifiers. The internal game-server endpoint requires the configured Dapr app token;
no account password is forwarded through that channel. Boards are projected from a
deep copy so reading does not roll over or create live state, and only an actually
saved referral code can be shared. Claims require that exact owned character to be
connected in the game, recheck its identity under the player persistence lock, and
delegate to the existing claim/invitation methods. Full bags remain claimable under
those existing rules. Do not describe this as a new transactional/offline delivery
system. Multi-process mission-cache coherence and crash-time reward delivery are
inherited from the existing engine and have not been certified by the website tests.

Nhiệm vụ cá nhân, thưởng chuỗi ngày, lịch điểm danh và tiến trình mời bạn dùng cùng
bộ quy tắc trong game. Trang xác thực tài khoản và kiểm tra nhân vật thuộc tài khoản;
máy chủ game kiểm tra lại trước khi thực hiện. Xem tiến trình dùng bản sao, không
làm mới hay tạo dữ liệu đang chạy. Chỉ chia sẻ mã mời đã thực sự được lưu. Nhận thưởng
hoặc lưu người mời cần nhân vật đã chọn đang trong game. Nếu túi đầy, phần thưởng
vẫn có thể nhận lại theo quy tắc hiện có. Chưa xác nhận khả năng đồng bộ bộ nhớ đệm
nhiệm vụ giữa nhiều tiến trình hoặc xử lý sự cố đúng lúc trao thưởng trên máy chủ thật.
The widgets use the account's current language and in-memory credentials. Only choosing
the subscription button requests browser notification permission. No tickets or
passwords are stored in browser storage.

Trang tài khoản dùng phần nền từ PR #51. Mục **Coin** hiển thị và nhận thưởng mốc nạp;
mục **Cộng đồng** cho phép đăng ký thông báo sự kiện trên trình duyệt. Trình duyệt chỉ
hỏi quyền thông báo khi bạn chủ động bấm nút đăng ký. Đăng nhập tài khoản một lần là
có thể dùng nút **Chơi ngay** có sẵn.

Coin history now reads bounded pages directly from persistence, including retained
entries beyond the previous 1,000-entry ceiling. New cash-shop purchases and gifts save
an already-applied negative receipt in the same save as the balance and storage changes.
A failed purchase removes that receipt before any later save. It is never a pending
grant, so it cannot deduct coins again. Purchase names are saved in English/Vietnamese
and rendered as text. Purchases made before recording was added cannot be reconstructed.
New direct balance changes also record their actual signed delta: extra vaults, item-market
and player-store purchases/sales, reset/relife/stat/master costs and play-time rewards.
Receipts are already applied and cannot become a second payment. These small hooks retain
the existing save and rollback boundaries. In particular, player-store seller credits use
the seller's normal progress save; this does not add atomic cross-account settlement.
Admin history localizes these reasons and shows Sydney time. Live PostgreSQL settlement
and crash recovery in the inherited payment paths have not been certified.

Confirmed email changes are configured at **Accounts → Email delivery** (`/email-settings`).
Outgoing SMTP is off by default. Configure a verified sender and STARTTLS submission
(usually port 587); implicit TLS on port 465 is not supported by this sender. A blank
replacement password keeps the encrypted stored credential. Back up the existing
data-protection key ring together with the server state. SMTP configuration uses the
`websiteMail` section of `save/hnmo-state.json`; per-account challenges use the existing
PostgreSQL feature-row backend. Only a salted hash of the random 24-character code is
stored, and the new mailbox is protected with the server keys. No game password or
confirmation code is saved in browser storage or logged.

The player's Security form requests a code and explicitly confirms it after delivery.
The server reserves the offline account and authenticates again before either mutation.
Codes expire in fifteen minutes, are bound to the authorizing password/current mailbox,
permit five incorrect attempts and can be resent after a minute. An `applying` intent
is persisted before updating the account; a retry can finalize a saved email whose final
challenge write failed. Consumed codes cannot be replayed. Tests use a fake sender;
delivery through a real provider has not been certified.

Thiết lập gửi thư tại **Tài khoản → Gửi email** (`/email-settings`); mặc định tính năng
đang tắt. Dùng địa chỉ gửi đã được xác nhận và cổng STARTTLS (thường là 587). Người chơi
thoát game, mở Bảo mật, lấy mã gửi đến email mới rồi nhập mã để xác nhận. Mã có hiệu lực
15 phút, dùng một lần, tối đa năm lần nhập sai; lấy mã mới cách nhau một phút. Mật khẩu
SMTP và email chờ xác nhận được mã hóa bằng khóa bảo vệ dữ liệu của máy chủ. Hãy sao lưu
cả khóa này. Không lưu mật khẩu game hoặc mã xác nhận vào bộ nhớ trình duyệt hay nhật ký.
Các kiểm thử dùng bộ gửi thư giả; chưa xác nhận gửi thành công qua dịch vụ email thật.

Lịch sử coin được chia trang trực tiếp từ dữ liệu còn lưu, không còn bị giới hạn ở
1.000 dòng mới nhất. Các lần mua hoặc tặng trong cửa hàng được ghi cùng lúc với số dư
và vật phẩm trong kho cửa hàng. Mua thất bại không để lại giao dịch trừ coin; dòng lịch
sử không trừ coin thêm lần nữa. Những lần mua trước bản cập nhật chưa có dữ liệu để
hiển thị. Các thay đổi số dư từ bản cập nhật này cũng được ghi: mua thêm rương, mua/bán
ở chợ và cửa hàng người chơi, phí reset/relife/tẩy điểm chỉ số hoặc master, và thưởng
thời gian chơi. Dòng lịch sử ghi đúng số coin tăng hoặc giảm thực tế, không trừ hay
cộng lại coin. Các điểm lưu và hoàn tác cũ được giữ nguyên; riêng coin người bán ở
cửa hàng được lưu theo lần lưu tiến trình thông thường. Chưa xác nhận thanh toán
trực tiếp trên PostgreSQL hoặc khôi phục sau sự cố của các luồng thanh toán cũ.
Lịch sử trong admin có tên giao dịch bằng tiếng Việt và thời gian Sydney.
