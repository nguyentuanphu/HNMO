# The five browser tests that failed only in a Season 9 world (2026-11-20, Sydney)

Job 10 of `docs/prompts/local-claude-overnight-2026-11-19.md`. Job 3 (`docs/s9-run-2026-11-20.md`, at `d32e8255`) saw
five tests pass in Season 6.3 and fail in a Season 9 world: the elf's, the lord's (`plasma-storm`) and the
summoner's (`chain-lightning`) skill effects, "debuffs colour a monster", and the Dark Knight's new skill buff (180).

## Result: none of the five is a Season 9 problem

Four never failed again, in any order. The fifth (new skills) was a walk that ended short, the bug #127 fixed; with
#127 it passes in both seasons. Season 9's `Player/player.bmd` (293 actions against 284) was in use in every run below,
so the action-count idea isn't the cause and nothing needs changing for it.

## What I ran

Setup each time: demo server, bridge and development client built from one commit; the bridge's
`HNMO__S9DataPath` at the Season 9 pack (`<scratch>/s9-pack`); the demo world set to Season 9 on `/worlds`; checked
that the game loads Season 9 data (515 requests to `/data/s9/`, among them `Player/player.bmd`); the repository's
Playwright config, one worker, `--trace off`.

| commit | run | result |
|---|---|---|
| `92b12703` (no client, bridge or server change since Job 3's `d32e8255`) | `skill-effects.spec`, `new-skills.spec` alone | **10 passed** |
| `0f0d6362` | Job 3's 28 files, Job 3's order (50.9 min) | **51 passed, 3 failed, 2 skipped**. All four skill-effects cases of the five passed (elf, lord, summoner, debuffs). Failed: new-skills (line 36, buff 180), pet-zen (line 28), inventory's vault test (line 89) |
| `0f0d6362` | the three failed files alone | 5 passed, 1 failed: new-skills again; pet-zen and the vault test passed |
| `6b77aead` (with #126 and #127) | `new-skills.spec` ×3 in Season 6.3, then ×3 in Season 9 | **3 passed, 3 passed** |
| `6b77aead` | Job 3's 28 files again in Season 9 (50.0 min) | **53 passed, 1 failed, 2 skipped**: all five pass. Failed: rage-fighter's Dark Side (line 223, "more than one monster or NPC in view") |
| `6b77aead` | that test alone ×2 in Season 9 | 1 passed, 1 failed (same line) |

The Dark Side failure is not a Season 9 one either: after `/move lorencia` the test needs two monsters or NPCs in
view, and `/move` lands on a random cell of the town's arrival area; on the north bridge only one is in view. It
passed in the first full run and in Season 9 before (`docs/s9-run-2026-11-20.md`).

Why new-skills failed: its hero walks out of town (`/move lorencia`, then to (150,100) and (155,85)) and casts Iron
Guard, which the server refuses in a safe zone. The failure screenshots show the hero still in town or on the north
bridge ((135,97), (135,98), (135,106)): the walk had ended short, so the cast was refused and buff 180 never came.
That is the walk bug fixed in #127 (the server puts the hero back when the next part of a walk starts ahead of it,
and the client used to drop the walk); `0f0d6362` doesn't have #127, `6b77aead` does. (150,100) is not a walkable
cell in either season's Lorencia, so the first walk always does nothing; the second one leaves town.

pet-zen's failure is the one #126 fixed (it was at an earlier line here because the run's bag state differed). The
vault test passed alone; Job 3 also couldn't decide it.

## Found on the way

Lorencia's cell flags differ between the base data and the Season 9 pack in **2,109 cells** (walkable in one, not
in the other; decoded with the web game's own reader). In a Season 9 world the client walks on Season 9's map; the
server's maps are HNMO's own. I didn't check which cells or whether the server's walk checks disagree with the
client there; worth a look if players see walks stopping in Season 9 maps.

## Changes

None. Nothing to fix for these five beyond #126 and #127, which are merged.
