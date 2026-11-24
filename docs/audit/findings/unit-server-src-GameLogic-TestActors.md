# Unit Audit: `server/src/GameLogic/TestActors`

- **Audit date**: `2026-11-24`
- **Status**: `Checked` (27 files read in full)

## Summary

- **Actor Lifecycle & Protocol (`ActorRegistry`, `ActorProtocolHandler`, `ScriptedPlayer`, `BotsController`)**:
  - `ActorRegistry.SpawnAsync` checks all server contexts in the process plus `LoginServer.TryLoginAsync` under `_lock` to prevent duplicate account sessions.
  - `ActorProtocolHandler` speaks newline-delimited JSON (`ping`, `spawn`, `stop`, `list`, `bots`, `state`, `nearby`, `halt`, `events`, `walk`, `attack`, `skill`, `say`, `pickup`, `warp`) with strict integer bounds checking.
- **Execution & Recording (`ScriptedIntelligence`, `RecordingViewPlugInContainer`, `ActorEventLog`, `ActorHitRecorderPlugIn`)**:
  - `ScriptedIntelligence` executes commands serialized via `RunPersistenceExclusiveAsync` with chunked walks (`MaxStepsPerWalk = 16`) and interruptible delays.
  - `ActorEventLog` keeps a 4096-event ring buffer and bounded 1024-event `DropOldest` channels with `lag` markers so slow followers never block game threads.

No defects found.
