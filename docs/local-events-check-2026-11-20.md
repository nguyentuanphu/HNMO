# Job 4 (events countdown): what the local Claude found before main Claude took it (2026-11-20, Sydney)

Main Claude took Job 4 (#104). These are the local findings, for the record; no code change.

Setup: demo server and development client from `92b12703`, CI's Playwright config (SwiftShader), one worker, on the
owner's Windows PC.

- `tests/e2e/events.spec.ts` on `92b12703` alone: **failed at line 42**, before the countdown: the Blood Castle entry
  window (`.rml-blood_castle_enter #panel`) never opened. The test walks right after `/move devias`; that is the
  map-change race fixed in #110 (Job 1: a walk asked for while the map changes was dropped by the server and the
  correction ended the walk, so the hero never reached the Messenger of Archangel).
- The same commit with #110 merged locally (not pushed): **passed 4 times out of 4** (1.2 minutes each), the
  countdown line included (`Blood Castle Event ends (in 30|29|28 seconds)`).
- Read in the code (`client/web/src/game/events.ts`, `src/ui/eventWindows.ts`): `C1 92 05` gives countdown type 6
  (Blood Castle "Event ends"), `countdown()` builds the text every frame for 30 seconds, and `CountdownLine` opens
  `hud_status.rml` on the first non-empty text; nothing else opens that document. I found nothing wrong there; a line
  that only shows after its document opened (asynchronously) can miss a fast check on a slow machine, which is what
  #104 addresses.
