# Bug reports (2026-11-20, Sydney)

## The owner's decisions (pop-up, 2026-11-20 afternoon)

- Players report bugs from the game; the server's owner handles them on the admin panel and may send a report on to the
  HNMO developer.
- Reports reach the HNMO developer through **the developer's own Discord channel**: the developer gives each server
  owner a Discord webhook link, which the owner pastes into Plugins → "HNMO bug reports" → "Developer webhook URL".
  If a link leaks, the developer makes a new one in Discord and hands it out again.
- **No screenshots** in reports.
- **Rewards are the server owner's choice**: off by default; the owner switches them on and picks the coin and amount.

## How it works

- Player: Alt+B (phones: 🐞 in the ⋯ menu) opens the window (`client/web/src/ui/bugReportWindow.ts`): kind (display,
  skill, item, quest/event, connection, other), text (10-1000 characters), up to 5 a Sydney day per account. The game
  adds the page, browser, screen, touch, language, GPU, map, server and the last 10 page errors; never the address's
  hash (it can hold a login ticket). Lines naming a password or ticket are dropped by the server too.
- Packets: F5 54 `HnmoBugReportCommand` (C2; Action 0 state, 1 send with ASCII JSON) and F5 55 `HnmoBugReportState`
  (C2 JSON). Works in the Arena too.
- Server: `GameLogic/PlugIns/BugReports` (`BugReportEngine` pure rules, `BugReportStore` in the state file section
  "bugReports", `BugReportsPlugIn`, `BugReportForwarder`). Each program (Season 6.3, Season 9, Arena) keeps its own
  reports, on its own admin panel.
- Admin panel: "Bug reports" (`/bug-reports`): filter, Confirm / Fixed / Not a bug / Duplicate (of another id),
  answer, "Send to the HNMO developer". A confirmed or fixed bug gives the reward once (coins history: "HNMO bug
  reports"). The player sees the status and answer in the window, and as a chat line at once (online) or when entering
  the game.
- What the developer receives: id, kind, text, game server, map and position, status, the technical details, the
  owner's answer and the server's license name ("unlicensed" without one). No login, e-mail or IP; the character only
  if the owner ticks "Share character names".

## Checked (2026-11-20)

- Unit tests: daily limit across the Sydney midnight, statuses and answers seen once, duplicates, the Discord message
  without the login, the webhook check, sending through a fake HTTP handler, a player's send through the plug-in.
- Live on PostgreSQL (Season 6.3, test7 on game server 1):
  - the window sent B1-B4 (with Vietnamese text and a recent page error in the details), counting down 5 → 1 left;
  - the admin page listed them, confirmed and answered; the player, online on another game server, got
    "Bug report B4 was confirmed." in the chat at once, and the older answers when entering the game;
  - with the reward on (100 WCoinC) a confirmed report gave 100 WCoinC once (coins history "HNMO bug reports");
  - "Send to the HNMO developer" reached Discord (a made-up webhook link, so Discord answered 404 and the page said
    "Not sent"); without a link the button is off.
- Not checked: a post to a real Discord channel (needs the developer's webhook link).
