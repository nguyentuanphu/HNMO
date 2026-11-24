# Unit: server/src/GameServer/MessageHandler (files 1)

Checked against: rule scan across `96` root packet handler files (`4924` lines) and Season 6/9 Summoner Pollution skill behavior.

Scope: `server/src/GameServer/MessageHandler (files 1)` (`96` files, `4924` lines).

## Summary

- `95` files are pure C->S packet decoders delegating to `GameLogic` actions.
- One handler embeds a skill repeat rule: `AreaSkillAttackHandlerPlugIn.cs:26,54-68`.

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `AreaSkillAttackHandlerPlugIn.cs:26,54-68` | `PollutionSkillId = 225`: after initial cast, runs `5` delayed extra hits separated by `1000ms` | Season 6/9 Summoner Pollution multi-wave ground tick | Matches multi-wave role (timing implemented in handler) |
