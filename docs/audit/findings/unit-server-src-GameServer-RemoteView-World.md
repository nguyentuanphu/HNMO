# Unit: server/src/GameServer/RemoteView/World

Checked against: Season 6 Crywolf altar NPC IDs & legacy 0.75 effect flags.

Scope: `server/src/GameServer/RemoteView/World` (`52` files, `4090` lines).

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `CrywolfEventViewPlugIn.cs:29,52` | `FirstAltarKey = 317` (altar NPC index offset); clamps statue shield percentage to `0..100` | Season 6 Crywolf Elf Altars (#205..209 / wire key `317+`) | Same |
| `EffectNumbers.cs:15-40` | Legacy 0.75 effect bitflags: `DamageBuff=0x01`, `DefenseBuff=0x02`, `Poisoned=0x37`, `Iced=0x38`, `DuelSpectatorHealthBar=0x62` | Legacy 0.75 / early MU protocol effect flags | Same |
