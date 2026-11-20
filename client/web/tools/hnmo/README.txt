HNMO
====

Everything in this folder: the game, its server and admin panel, and a small
database that keeps your progress. You play in a web browser. The same folder runs on
Windows, Ubuntu/Linux and macOS (runtime/ has what each system needs).

START
  Windows: double-click "Start HNMO.bat". No window stays open: HNMO's icon appears
           next to the clock (right-click it: open the game, the admin panel, the log, or
           Stop HNMO). Your progress is saved. To see a problem at start in a window,
           use "Start HNMO (window).bat" instead.
  Ubuntu/Linux: once, install PostgreSQL 16: sudo apt install postgresql-16
           Then run ./start-hnmo.sh: HNMO runs in the background (the Terminal can be
           closed). Stop it with ./stop-hnmo.sh or "Stop HNMO" in the admin panel.
  macOS:   once, install PostgreSQL 16: brew install postgresql@16 (or Postgres.app),
           and allow the folder: xattr -dr com.apple.quarantine HNMO
           Then run ./start-hnmo.sh in Terminal: HNMO runs in the background (the
           Terminal can be closed). Stop it with ./stop-hnmo.sh or "Stop HNMO" in the
           admin panel.
  The log (what the old window showed): the admin panel's "Server log" page, or
  save/logs (launcher.log, server.log, database.log).
  (Windows needs nothing installed: its database is in the folder.)

  The website opens in your browser at http://localhost:8080/ when it's ready: server
  information, rankings, the event schedule, accounts. The game loads in the background;
  "Enter the game" starts it (also at http://localhost:8080/index.html). The very
  first start sets up the save game and takes up to a few minutes.

  Windows may warn "Windows protected your PC" (the files aren't signed):
  click "More info" and "Run anyway". (Tiếng Việt: Bấm "More info" -> "Run anyway".)
  macOS Gatekeeper may say the app is from an unidentified developer:
  right-click HNMO.app (or HNMO), choose "Open", and click "Open" again. (Tiếng Việt: Chuột phải -> "Open" -> "Open".)
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

PLAYERS AT ONCE
  Up to 10 players can be in the game at once. A license file from the owner
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
