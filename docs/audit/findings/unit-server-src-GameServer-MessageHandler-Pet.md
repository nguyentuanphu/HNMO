# Unit: server/src/GameServer/MessageHandler/Pet

Checked against: Season 6/9 Dark Raven command skill-slot encoding.

Scope: `server/src/GameServer/MessageHandler/Pet` (`2` files, `115` lines).

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `PetCommandRequestHandlerPlugIn.cs:29,51` | `SkillOffset = 120` modulo mapping client pet command mode (`Normal, AttackRandom, AttackTarget, AttackWithOwner`) | Classic Dark Raven command packet IDs (`120..123`) | Same |
