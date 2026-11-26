# Unit Audit: `client/src/source/GameLogic/NPCs`

- **Unit**: `client/src/source/GameLogic/NPCs`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `8` (`374` lines) including `npcBreeder.cpp`, `npcCatapult.cpp`, `npcGateSwitch.cpp`, `PlayerNpcChat.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — `npcBreeder.cpp` computes Dark Horse / Dark Spirit recovery Zen cost)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Trainer NPC Dark Horse / Dark Spirit recovery Zen formula | `npcBreeder.cpp:24-116` | Computes repair Zen from missing pet durability (`maxDurability - ip->Durability`), pet value/level, and compares `(int)CharacterMachine->Gold < Gold` before sending repair request | Server NPC Trainer repair / `RepairItemAction` | Same |
| Other NPC action files | `npcCatapult.cpp`, `npcGateSwitch.cpp`, `PlayerNpcChat.cpp` | Castle Siege catapult/gate switch trigger wrappers & player speech bubbles | Delegated to server / UI | No game rules |
