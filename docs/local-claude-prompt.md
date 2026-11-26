# Prompt for local Claude, 27 Nov 2026 10:30 Sydney

Antigravity owns the queue in `docs/antigravity-prompt-2026-11-27-1020.md`. Local Claude gets only work that touches
other files and other branches, and gives Antigravity the heavy lock first.

```
From main cloud Claude, Fri 27 Nov 10:30 Sydney. The owner moved your earlier queue (5c loader, #420/#421 reruns,
#423 barrier kills) to Antigravity. Don't start any of it. Here is separate work that can't clash with it.

## Don't touch (Antigravity or main is on them)
- Branches: integration/s9, feat/s9-pentagram (#428), fix/s9c-gameplay (#424), fix/asset-db-transactions (#423),
  feat/shared-coins-2pc, and the branches of #367, #381, #382, #386, #420, #421, #431, #432, #411, #422, #430.
- Files: client/src/source/Data/** (the desktop loader, Antigravity's 5c), server/src/GameLogic/OutlawPoints.cs,
  the JewelBank/AssetOperation files.
- heavy.ps1: Antigravity goes first. Queue at most one job, only when no Antigravity job is waiting or running, and
  stop if one is queued after yours. Never run a packaged folder while Antigravity has one running (ports 55432,
  8095, 44406, 8080).

## Your tasks, in order (each its own branch from origin/claude/hnmo-main-handover-54co2m, one draft PR each)
1. S9 server names on the start screen. On S9 the start screen shows only "Server 1..3"; S6 shows the full names.
   Find where the list gets its names (server config / connect server / web start screen), and make S9 show its names
   the same way S6 does. Follow the desktop client's server list for the format. Never hard-code the game name (use
   gameName()). Test: a unit test for the name, plus one real start-screen check on the packaged folder (light: start
   it only when the heavy lock is free). Branch fix/s9-server-names.
2. Review #432 (Antigravity's desktop build fixes, read-only): read the diff (ClientLibraryCodegen dependency, the
   5 regenerated Dotnet/Packet* files). Check the regenerated files match what the generator produces from
   server/src/Network/Packets on the base. Post the findings as a PR comment, and don't push to the branch. A cmake
   build only if the lock is free.
3. Review #431 (Antigravity's client audit docs, read-only): spot-check 10 values in docs/audit/data/client-s6/ and
   client-s9/ against your own local decoders (they stay outside the repo). Post matches and mismatches as a PR
   comment. Also check that no pack file and none of the forbidden names went into the PR.
4. If you're done before Antigravity needs the lock for long: the 15 Vietnamese item texts of #425 are merged. List
   any that read oddly in the game (screenshot from the web game). It's for the owner to decide, so change nothing.

Report: per task, what ran, what was verified and what wasn't, the PR links, Sydney times.
```
