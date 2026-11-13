---
name: hnmo
description: How the HNMO owner works and how to work on HNMO (server, web game, packaging). Use at the start of any HNMO task, and update it when you learn a new preference.
---

# Working on HNMO

## What the owner wants

- One HNMO folder with everything (server, admin panel, web game, save database)
  that starts with **one launch**; friends play in the browser, together over
  Tailscale or offline alone. Progress is saved in the folder.
- Everything named HNMO; nothing about the original game, publisher or base projects
  in anything pushed to GitHub (commit messages, PRs, docs, code). Keep license and
  copyright notices.
- Free cash shop coins (no real money); a simple account page to register and get coins.
- The web game should match the desktop game feature for feature (see
  `client/web/PARITY.md`), built in small, tested steps.
- Times in Sydney time. Short, plain reports: what works, what's verified, what's next,
  and what the owner has to do (with links).

## How to work

1. Read `CLAUDE.md`, then `client/web/PARITY.md` for what's done and what's next.
2. Make the change; build (`dotnet build server/src/HNMO.sln -c Release`) and run the
   tests that cover it.
3. Test in the real game: package with `client/web/tools/package-hnmo.sh`, run it as a
   non-root user, drive the browser with Playwright (headless Chromium with
   `--use-gl=angle --use-angle=swiftshader`), take screenshots of what changed.
4. Commit with an HNMO-only message; push; check the **HNMO package** workflow run and
   give the owner the download link.

## Things learned

- The connect server answers on 44406; the game server sends own player id 0x0200.
- Packet definitions: `server/src/Network/Packets/*.xml`; regenerate the web client's
  `packets.generated.js` with `client/web/tools/generate-packets.mjs` after changes.
- A fresh save has the test accounts; `test1` has four characters, `test300` one.
- The account page needs a one-time API key from the admin panel (see the folder's
  README.txt); handlers taking only `HttpContext` must be cast to
  `Func<HttpContext, Task<IResult>>`.
- Inside this sandbox, run local HTTP tests without the proxy
  (`env -u HTTPS_PROXY -u HTTP_PROXY -u https_proxy -u http_proxy`).
