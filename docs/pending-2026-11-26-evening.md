# HNMO pending list (26 Nov 2026, evening, Sydney time)

Replaces the state part of `docs/pending-2026-11-26.md`. The owner's answers of today are in `docs/owner-decisions.md`
(batch order, old PRs, Q1-Q13, Arena Home text, unsigned Mac app); open owner questions: `docs/owner-questions-pending.md`
on `claude/hnmo-main-handover-kpts7l`.

## Merged into the working branch today (all with the full server suite, the web checks and the launcher tests green)

- Batch A: #368, #372, #370, #369, #375, #374, #378, #371. Then `main` (two docs of 19 Nov), batch B: #365, #366, #383.
- #385 (sound and walk-stall specs), #389 (season column for the browser suites).
- Batch D/E: #373, #377, #376, #393 (coin sync undone when the local save fails), #394 (economy tests), #388 (Potion
  of Bless only against siege gates and statues), #392 (timetable spec race), #396 (economy guards: equipment is one
  piece in combining leftovers, one jewel per Jewel Bank withdrawal), #379 (Seasons page; conflicts resolved by keeping
  both sides), #380 (resizable chat windows, three Vietnamese texts added), #390 (local report), #395 (launcher misses at
  Debug level, at most 10,000 remembered), #387 (coin tests).
- #384 (macOS and Linux installers) with #391 (start script in Resources so the app can be sealed): the package run is
  green on Windows, Ubuntu x64 / ARM64, Mac Intel and Mac Apple Silicon.
- Closed after checking: #2, #3, #4, #5, #257, #277, #349, #351-#354.

## Open

| PR | State |
|---|---|
| #386 Gens: same gens for guild and alliance, opposite-gens chat hidden | taken out of the batch: `social.spec` could not make a guild without a gens; the spec now joins Duprian first; waiting for CI. Owner question 6 (apply to running servers?) |
| #400 Walking: next part of a long walk one step further ahead on the server's walk | the cause of `walk-stall`'s last step; waiting for CI |
| #367 Autoplay tiers | breaks `auto-play-town`, `ultimate-hud-bar`, `bottom-hud-bar` in its own run; Autoplay is Antigravity's (owner, 24 Nov) |
| #382 HUD reference sweep | breaks the HUD (`#hud_layout` missing); Antigravity |
| #381 monster parts, S9 NPCs, Arena | text fixed (Home walkable, not safe); its own in-game spec and FPS / heap numbers not run (local) |
| #13, #17 (base `main`) | owner's call |
| hub #2 | reviewed, `tsc` and 21/21 tests pass; owner's call (Netlify may publish) |

## Red browser specs still open (see `docs/browser-test-reds-2026-11-26.md`)

`small-ui-feedback.spec:16` and `trade.spec:48` (a game dialog never shows), the Fighter skill-effect specs
(intermittent), `walk-resume.spec:26` (flaky; 10/10 green locally), `skills.spec:14`, `skill-requirements.spec:45`.

## For the owner's PC

Prompts: `docs/local-claude-prompt.md` (local Claude, rounds 2-3) and `docs/antigravity-prompt-2026-11-26.md` on
`claude/hnmo-main-handover-kpts7l`. The DeferredTcpPipe self-check passes 3/3 on Linux; its 3/3 red is Windows only.
