HNMO
====

Everything in this folder: the game, its server and admin panel, and a small
database that keeps your progress. You play in a web browser. The same folder runs on
Windows, Ubuntu/Linux and macOS (runtime/ has what each system needs).

START
  Windows: double-click "Start HNMO.bat". No window stays open: HNMO's icon appears
           next to the clock (right-click it: open the game, the admin panel, the log, or
           Stop HNMO). Your progress is saved.
           Visible console window (troubleshooting): run `Start HNMO.bat /window` (or
           `--window`), set `HNMO_WINDOW=1`, or set `"ConsoleWindow": true` in
           `save/hnmo-settings.json`.
           (Tiếng Việt: Bấm đúp "Start HNMO.bat" để chạy ngầm có biểu tượng khay hệ thống;
           muốn hiện cửa sổ console hãy chạy `Start HNMO.bat /window` hoặc đặt `"ConsoleWindow": true`.)
  macOS:   extract HNMO-macos.zip and double-click "Start HNMO.app". Your save
           is under Library/Application Support/HNMO in your home folder.
           The portable HNMO.zip still supports Start HNMO.command and
           keeps saves inside its HNMO folder.
           (Tiếng Việt: Giải nén HNMO-macos.zip rồi bấm đúp "Start HNMO.app".)
  Ubuntu/Debian: open "HNMO-linux.deb" in the software installer, choose Install,
           then open HNMO from the applications menu. The first launch copies the
           files to `~/.local/share/HNMO` (or `$XDG_DATA_HOME/HNMO`).
           (Tiếng Việt: Mở "HNMO-linux.deb" bằng trình cài ứng dụng, chọn Cài đặt,
           rồi mở HNMO từ danh sách ứng dụng.)
  Other compatible Linux desktops: use the extracted HNMO.zip with
           ./start-hnmo.sh. PostgreSQL is included for x64 and ARM64 on Linux
           and macOS. Builds made without the bundled database can use an
           existing PostgreSQL installation.
  To stop on macOS or Linux, choose "Stop HNMO" on the local control page.
  The log (what the old window showed): the admin panel's "Server log" page, or
  save/logs (launcher.log, server.log, database.log).

  The website opens in your browser at http://localhost:8080/ when it's ready: server
  information, rankings, the event schedule, accounts. The game loads in the background;
  "Enter the game" starts it (also at http://localhost:8080/index.html). The very
  first start sets up the save game and takes up to a few minutes.

  Windows may warn "Windows protected your PC" (the files aren't signed):
  click "More info" and "Run anyway". (Tiếng Việt: Bấm "More info" -> "Run anyway".)
  macOS may ask you to approve a build downloaded from the official HNMO release.
  Open System Settings → Privacy & Security and choose Open Anyway for Start HNMO.
  (Tiếng Việt: Mở Cài đặt hệ thống → Quyền riêng tư & Bảo mật → Vẫn mở cho Start HNMO.)
  Control page: http://localhost:8080/control (this computer only: Start/Stop/Restart, logs, backups, updates).
  Windows 11 "Smart App Control blocked a file": Windows marks everything that came from
  the internet. Before unzipping: right-click HNMO.zip, Properties, tick "Unblock", OK,
  then Extract All again. Already unzipped: open PowerShell in the HNMO folder and run
    Get-ChildItem -Recurse | Unblock-File

SAVE GAME
  Progress is saved in the "save" folder: accounts, characters, items, guilds, coins.
  To back it up, stop the game and copy the "save" folder. To start over, stop the
  game and delete the "save" folder.

ACCOUNTS AND COINS
  On the website (Account section) or http://localhost:8080/account.html
  (friends use your address instead of localhost).
  - Sign in with Google: set a Google client id in the admin panel, Plugins,
    "Website (admin address, Google sign-in)".
  - Create an account: new accounts get free cash shop coins.
  - My coins: shows an account's coins.
  First, once, on the host computer:
    1. Open the admin panel (below), go to Users and create the first user.
    2. Open "API keys", create a key with the role "Administrator", copy it.
    3. Open http://localhost:8080/account.html and paste the key into the setup box.
  Test accounts also work: test0 ... test9, test300, test400 (password = name).

IN THE GAME
  Log in, pick or create a character (Dark Wizard, Dark Knight and Fairy Elf;
  the other classes unlock later in the game). Click to walk, right-drag to turn,
  mouse wheel to zoom. Press Enter to chat; "/move devias" travels to Devias.

ADMIN PANEL
  http://localhost:8095/  - on the host computer.
  Through the website: a random, hard-to-guess address made for this folder, shown on
  http://localhost:8080/control (an admin login is always needed there; it stays closed
  until an admin user exists). Change it in the admin panel, Plugins, "Website (admin
  address, Google sign-in)". Leave it empty to allow the admin panel only on the host computer.
  From afar the 6-digit code of an authenticator app is needed: set it up first on the
  host computer (admin panel, "2FA & Security"). After the code was right once, that
  browser is trusted for 30 days (only the password then); "Forget every trusted browser"
  on that page ends it everywhere. The same address opens every panel:
  Season 9's under ".../s9/", the Arena's under ".../arena/", one sign-in for all.
  From afar, Host settings shows only the licence (ports and folders: host computer only).

UPDATES
  Admin panel, "HNMO update": shows this folder's version and the newest one, with its
  notes. "Update to <version>" downloads only the files that changed, makes a backup of the save
  game, stops HNMO, puts the new files in and starts it again (a few minutes; players
  are disconnected meanwhile). The save game stays as it is. If the new version doesn't
  start, the old files and the save game from before the update come back by themselves.
  What happened is in save/logs/update.log; the backup is in save/backups.

PLAYERS AT ONCE
  Up to 10 players can be in the game at once, Season 6.3, Season 9 and the Arena
  together (an account playing in two of them counts once). A license file from the owner
  (license.hnmo) raises that: put it next to "Start HNMO" and restart. The server's log
  says what it allows ("HNMO license: ...") and this computer's id, which the owner needs
  for a license tied to this computer.

PLAY WITH FRIENDS
  One person runs this folder (the host); friends need only a web browser.

  Same Wi-Fi: friends open http://HOST-IP:8080/ (Windows: "ipconfig" shows the
  host's IPv4 address, e.g. 192.168.1.20). Allow "HNMO" through the firewall when
  Windows asks (Private networks).

  Friends elsewhere, without opening ports - Tailscale (free):
    1. The host and every friend install Tailscale (tailscale.com) and sign in.
    2. The host shares this computer with each friend: Tailscale admin console,
       Machines, the host's machine, "Share", and sends them the invite.
    3. Friends open http://HOST-TAILSCALE-IP:8080/ (the host's 100.x.y.z address,
       shown in the Tailscale app).

LICENSES
  HNMO: licenses/LICENSE.txt. Parts made by others: licenses/THIRD-PARTY-NOTICES.txt.
