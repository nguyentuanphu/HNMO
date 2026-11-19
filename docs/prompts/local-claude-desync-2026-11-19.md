# For the local Claude working on movement lag (2026-11-19 20:20 Sydney)

Paste everything below the line.

---

On HNMO, branch from `origin/claude/hnmo-main-handover-54co2m` (or merge it into your movement branch). Main Claude
found a movement bug that blocks the release; it is in your area (character lag), so please fix it there.

**The bug:** sometimes the player's browser walks, but the server never moves the player, and nothing corrects it.
The player's own screen shows them at the new cell; everyone else sees them stuck where they were. Later clicks to
the same cell do nothing (the browser thinks it is already there), so it never recovers.

**Seen in:** `client/web/tests/e2e/command-window.spec.ts` (test1 walks next to test0). Logged positions in a failing
run: test1's own screen at (147, 138), test0 sees test1 at (138, 130) for the rest of the test. The server logged no
"Client reported a walk position ... Keeping the server position" warning for it, so the walk was not refused with
a correction: it was dropped silently.

**Where to look (server):**
- `server/src/GameLogic/PlayerMovement.cs`, `WalkToAsync`: several early `return`s send nothing to the client
  (`CurrentMap is null`, no attributes, frozen/stunned/asleep, empty steps). Only the refused cases call
  `RejectWalkAndResynchronizeClientAsync`.
- `server/src/GameLogic/PlayerMapTransitions.cs`: a teleport (`/move`, `WarpToAsync`) sets `CurrentMap = null` until
  the client's ready packet (F3 12) is handled. A walk that arrives in that gap hits the first silent `return`.
- The web client (`client/web/src/game/online.ts`, `walkTo` / `walkNextPart`) moves the hero locally as soon as it
  sends the walk; it is only corrected by the server's instant move (`ObjectMoved`, handled in `moved()`).

**Expected:** whenever the server does not apply a walk, the player's browser is put back where the server has the
player (for example, resynchronise in every early return where a client is connected, and after a map change once the
player is on the map). Add a server unit test for each path, and run `tests/e2e/command-window.spec.ts` five times in a
row against one demo server: it must pass every time without changing the test.

**Rules:** HNMO name only, no third-party names in code, commits or docs; Sydney time; never weaken or skip a test.
Push your branch and open a draft PR into `claude/hnmo-main-handover-54co2m`; main Claude reviews and merges it.
