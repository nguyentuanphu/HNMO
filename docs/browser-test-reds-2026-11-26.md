# Red browser tests of the working branch (traced on 26 Nov 2026, Sydney time)

Source: the failing specs of the last seven completed "Web game tests" runs on GitHub (run ids and heads below), counted
from the job logs. The base of every run is the working branch at `99977d5e` (docs only since `8cb8539b`) plus the PR's own change.

| Spec | Runs red (of 7) | Cause |
|---|---|---|
| `timetable.spec.ts:39` | 7 | **Test race, fixed in this PR.** Pressing H asks the real server, whose reply is "off" (the Timetable plug-in is off by default); the test feeds its own state right after, and when the real reply arrives later it replaces it ("turned off by the server administrator"). The test now waits for the window to show the server's answer before it feeds its state. The second feed (guide button) is only checked for visibility. Not run here: needs the whole stack; CI shows it. |
| `small-ui-feedback.spec.ts:15/16` | 6 | red on the base, not traced yet |
| `trade.spec.ts:48` | 5 | red on the base, not traced yet |
| `sound.spec.ts:28`, `walk-stall.spec.ts:25` | 5 | fixed by #385 (a page call took 14.5 s on the loaded page; the checks now run in one call); #385's own run has both green |
| `skill-effects-cast.spec.ts:53` / `skill-effects.spec.ts:248` (fighter) | 4 | intermittent: absent from the two oldest runs and from #385's run, present in four runs of unrelated PRs. The effect keys come from ticks of a timed effect system (`dragon-slasher makes joint:groundWind` was missing) on a loaded runner. Needs a rerun on the working branch to count it; not traced to a PR. |
| `walk-resume.spec.ts:26` | 3 | flaky (correction counted only within 2 s or 3 frames, `online.ts:256`) |
| `website-information.spec.ts:12` (vi) | 1 (the oldest) | fixed by #368; green in the batch A run |
| `movement`, `minimap`, `new-skills` | 2 | appear together in the runs of PRs stacked on #377 and in an old run: look at them when #377 is reviewed (anticheat speed / time change) |

Runs: 45751471122 (#388), 45749523109 (#387), 45748831177 (#386, first head), 45747669704 (#384), 45746225607 (#385),
45716182179 (#384 old head), 45514919814 (#383).
