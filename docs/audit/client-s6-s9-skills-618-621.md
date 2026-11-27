# Desktop Client Data Audit: Season 9 Skills 618–621

Audited on `2026-11-27` (Sydney time) from the Season 6.3 and Season 9 desktop client `Data` packs using
`tools/local-checks/desktop-data/master_tree.py` and `tools/local-checks/desktop-data/skills_buffs.py`.

## Summary

- **Master Tree (`MasterSkillTreeData.bmd` & `MasterSkillTooltip_eng.bmd`):** Neither the Season 6.3 client
  (`293` rows, skills `300–608`) nor the Season 9 client (`472` rows, skills `300–616`; `299` non-zero
  tooltip records `300–617`) has any master tree entry for skills `618–621`. No scroll, orb, or parchment in
  Season 9 `Local/Eng/Item_eng.bmd` teaches `618–621` either.
- **Skill Records (`Local/Eng/skill_eng.bmd`, Season 9, 112-byte records `618–621`):** All four records exist
  in Season 9 `skill_eng.bmd` and duplicate records `210–213` (empty in Season 6.3 `skill_eng.bmd` at slots
  `618–621`, while Season 6.3 slots `210–213` carry the same names with `Delay = 0`).
- **PR `#424` (`fix/s9c-gameplay`) Discrepancies:**
  1. `Mana` is **`30`** for **all four skills (`618–621`)** in Season 9 `skill_eng.bmd`. PR `#424` set `mana = 0`
     for `618–620` in `client/web/public/data/Season9/s9-skills.json` and `SeasonNineSystemsUpdatePlugIn.cs`.
  2. Effects and buff IDs for `618–620` come directly from the client's `CursedTempleSystem` / Illusion Temple
     buff definitions for slots `210–213` (`BuffEffect_eng.bmd` rows `33` and `64`, `_enum.h` `326–330` and
     `4037`/`4071`, `CursedTempleSystem.cpp:135-205, 1159-1295`).
  3. Record `621` uses the spelling typo `"Shied-Burn"` in Season 9 `skill_eng.bmd` (server/web keep `"Shield-Burn"`).

## Exact Desktop Client Records

### 1. `Local/Eng/skill_eng.bmd` (Season 9, 650 × 112 bytes + 4-byte checksum)

| ID | Record | Name | Level | Damage | Mana | AG | Distance | Delay (ms) | Energy | Leadership | TypeSkill | Classes `[DW,DK,FE,MG,DL,SUM,RF]` | Tail `b[88..91]` |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 618 | `skill_eng.bmd[618]` | `Spell of Protection` | 0 | 0 | **30** | 0 | 0 | 60000 | 0 | 0 | 1 | `[1, 1, 1, 1, 1, 1, 0]` | `[2, 1, 0, 0]` |
| 619 | `skill_eng.bmd[619]` | `Spell of Restriction` | 0 | 0 | **30** | 0 | 3 | 60000 | 0 | 0 | 2 | `[1, 1, 1, 1, 1, 1, 0]` | `[0, 0, 0, 1]` |
| 620 | `skill_eng.bmd[620]` | `Spell of Pursuit` | 0 | 0 | **30** | 0 | 0 | 120000 | 0 | 0 | 0 | `[1, 1, 1, 1, 1, 1, 0]` | `[2, 0, 0, 0]` |
| 621 | `skill_eng.bmd[621]` | `Shied-Burn` | 0 | 0 | **30** | 0 | 3 | 60000 | 0 | 0 | 0 | `[1, 1, 1, 1, 1, 1, 0]` | `[0, 0, 0, 1]` |

*(Note: Season 9 `skill_eng.bmd[210..213]` are byte-for-byte identical to `[618..621]`.)*

### 2. Buff IDs and Client Cast / Visual Behaviour

| Skill | Client Enum (`_enum.h`) | Buff ID (`BuffEffect_eng.bmd`) | Buff Name & Description | Client Effect on Cast (`CursedTempleSystem.cpp`) |
|---|---|---|---|---|
| 618 | `AT_SKILL_CURSED_TEMPLE_PRODECTION` (210) | **34** (`row 33`, `eBuff_CursedTempleProdection`, `:4037`) | `Spell of Protection`: `"Damage absorbed at 100%%."` | Caster plays `PLAYER_ATTACK_REMOVAL`. Registers buff `34` on target; spawns `MODEL_CURSEDTEMPLE_PRODECTION_SKILL`, `MODEL_SHIELD_CRASH`, `BITMAP_SHOCK_WAVE` (`Light 0.3, 0.3, 0.8`). Unregisters effect when buff ends. |
| 619 | `AT_SKILL_CURSED_TEMPLE_RESTRAINT` (211) | **65** (`row 64`, `eDeBuff_CursedTempleRestraint`, `:4071`) | `Spell of Restriction`: `"Mobility disabled"` | Caster plays `PLAYER_ATTACK_REMOVAL`. Stops target (`Movement = false`, `SetPlayerStop`), registers debuff `65` (blocks movement/attack/dialog checks in `ZzzInterface.cpp:1950, 3062`, `ZzzObject.cpp:6960-6972`, `CustomMessageBox.cpp:968`); spawns `BITMAP_SHOCK_WAVE`, `MODEL_SHIELD_CRASH`, `MODEL_CURSEDTEMPLE_RESTRAINT_SKILL`, and 40× `BITMAP_JOINT_ENERGY`. |
| 620 | `AT_SKILL_CURSED_TEMPLE_TELEPORT` (212) | *(None)* | *(No buff entry)* | Caster plays `PLAYER_ATTACK_REMOVAL`; no client-side visual buff — server teleports caster to target (relic carrier in Illusion Temple / Battle Core pursuit). |
| 621 | Slot 213 replaced `AT_SKILL_CURSED_TEMPLE_SUBLIMATION` | *(None)* | *(SD drain instant effect)* | `ShieldBurnSkillPlugIn` in `#424` matches `Mana=30, Distance=3, Delay=60000, Classes=[1..1,0]`; legacy slot 213 cast visuals in `CursedTempleSystem.cpp:184-205` used `MODEL_SKILL_INFERNO`, `MODEL_SHIELD_CRASH`, `BITMAP_SHOCK_WAVE`, `PLAYER_SHOCK`. |

## What HNMO Must Change

1. **Skill Data (`#424`):** Update skills `618`, `619`, and `620` in `SeasonNineSystemsUpdatePlugIn` and
   `client/web/public/data/Season9/s9-skills.json` from `Mana = 0` to **`Mana = 30`** (and keep `Delay = 60000`
   for `618/619/621`, `120000` for `620`; `Range = 0/3/0/3`; no Rage Fighter class).
2. **Skill Effects (`618–620`):** Wire `618` to apply Buff `34` (`Damage absorbed at 100%` / zero incoming damage),
   `619` to apply Debuff `65` (root / movement disabled), and `620` to teleport to target (matching client cast).
3. **Learning Source (Owner Decision):** Because `MasterSkillTreeData.bmd` contains no rows for `618–621`, HNMO's
   server/web master tree must either place them explicitly under owner direction (beyond S9 `MasterSkillTreeData.bmd`'s
   `300–616`) or treat them as granted/event-only skills.
