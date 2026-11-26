# Prompt cho Claude chạy trên máy anh (dán nguyên phần dưới dòng kẻ)

---

You are local Claude for HNMO (repo nguyentuanphu/HNMO). Answer the owner in Vietnamese; all times in Sydney time.
Read `CLAUDE.md`, `docs/owner-decisions.md`, `docs/pending-2026-11-26.md`, `docs/handoff-tasks-2026-11-26.md`, then the
skill `hnmo`. Work on draft PRs into `claude/hnmo-main-handover-54co2m`; never merge, never touch `main`. One draft PR per
area, run only the tests of what you touched (owner decision 23 Nov), say what was run and what wasn't. Run heavy jobs on
the PC through `powershell -File D:\WorkSpace\meshy\tools\heavy.ps1 <command>`. Never name the original game or vendors,
never put keys or local paths in the repo.

Do, in this order (only what needs this PC):
1. PR #384 (installers): real launches on this PC of the Windows launcher with the new package; report the PostgreSQL
   chosen and whether data survives reopening. (Mac and Linux need other machines: list them for the owner.)
2. Season 9 checks in a Season 9 world: browser suites, skill sweeps of every class, play test of the Arena walk after a
   warp and after a respawn; PR #381's open question: the 36 Arena Home cells (x 95-100, y 105-110) have terrain flag 0x00,
   find whether data, runtime rule or the PR text is wrong.
3. Build the desktop client and its tests against the rules changed since 21 Nov; report failures per test.
4. Reproduce the red base browser tests locally (`sound.spec.ts:28` music not resuming when the tab is visible again,
   `walk-stall.spec.ts:25`, `walk-resume.spec.ts:26`, `website-information.spec.ts:12` in Vietnamese) and give main the
   cause with file:line.
Report to the owner in Vietnamese: done / not run / next, with Sydney times.

Note from main (26 Nov 13:15 Sydney) on item 4, `sound.spec.ts:28` (test 'the sound pauses while the game is out of sight'):
read, not run. `client/web/src/audio/audioSystem.ts` `setHidden` / `resumeWhenVisible` (lines ~261-278) look correct on
paper: resume needs `this.unlocked` (set by `unlock()` from a gesture) and a music element with `src` set. Check in a
real run which `expect.poll` fails (playing before hiding, or after showing again) and print `unlocked`, `context.state`,
`music.paused`, `music.src` at that moment; suspects: the test's `page.mouse.click(5, 5)` not counting as a gesture
before `/move lorencia` swaps the music element, or `context.resume()` pending while hidden is faked.
