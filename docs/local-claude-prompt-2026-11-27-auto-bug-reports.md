# Prompt for local Claude: automatic bug reports (27 Nov 2026, Sydney)

```
From main cloud Claude, Fri 27 Nov (Sydney). Owner decision: automatic error reports go to the HOST only; the host
forwards what matters to the developer (no automatic sending to the hub). Rules: CLAUDE.md (set HNMO_DEVELOPER_HUB=off
before running the server in tests), strict TypeScript, parity rules (this is an HNMO extra: mark it in PARITY.md and
guide.html), draft PR into claude/hnmo-main-handover-54co2m, never main, Sydney times. Main cloud Claude merges.

## A. Automatic error reports (web game -> host)
- Client (client/web/src/ui/bugReportWindow.ts already keeps recent errors via installErrorLog): on an uncaught error,
  unhandled rejection, a packet handler failure (net/connection.ts), WebGL context lost, or an unexpected disconnect,
  send an automatic report to the server. Fingerprint = message + file + line (strip query strings/numbers in paths);
  each fingerprint at most once per page load; at most 5 automatic reports per browser per hour (localStorage, wrapped
  in try/catch). Payload: fingerprint, message, top stack lines, game version/build stamp, browser/OS/device, map,
  WebGL renderer — never the login, ticket, password or chat text.
- Packets: next free HNMO F5 codes (check the packet XML; regenerate with tools/generate-packets.mjs).
- Server (GameLogic/PlugIns/BugReports): store automatic reports grouped by fingerprint (count, distinct accounts,
  first/last seen in Sydney time, versions, last 5 samples); a per-account and per-server cap per Sydney day; no coins.
  Keep login only server-side for the cap (never shown with the stack).
- Admin panel "Bug reports": a new "Automatic" tab (EN, VI, DE, zh-CN): table by fingerprint sorted by count, details,
  "Mark fixed", "Ignore", and "Forward to developer" (reuses the existing forwarding so it travels with the weekly
  report, no login/IP, character only if ShareCharacterNames). Admin settings: on/off, caps.
- Tests: server engine tests (grouping, caps, forwarding payload has no login), web unit test (fingerprint, rate
  limit), e2e: throw a test error in the page -> one automatic report appears on the admin page; a second identical
  error doesn't add a row.

## B. Reporting from a PC/Mac browser
- The owner says the browser already has the 🐞 bug button (on PC and Mac too, not only phones): keep it as the way to
  report and check it shows on desktop Chrome/Edge/Firefox/Safari at the normal window sizes; if it only shows with
  the touch controls, show it on desktop as well.
- Bug: on a Mac, Option+B gives event.key "∫", so `event.key.toLowerCase() === 'b'` (main.ts ~2241) never fires: use
  `event.code === 'KeyB'` (keep the other modifier checks).
- guide.html: "Report a bug: the 🐞 button (PC, Mac, phone) or Alt/Option+B".
- e2e: the 🐞 button opens the window on a desktop viewport; Alt+B via event.code opens it.

Report on each PR: what ran, what was verified and what wasn't (real browsers on Windows/Mac if you can), Sydney times.
```
