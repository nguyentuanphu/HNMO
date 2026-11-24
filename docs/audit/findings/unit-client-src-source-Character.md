# Unit: client/src/source/Character

Checked against: Web client `characterCalc.ts` and Season 6/9 classic character formulas.

Scope: `client/src/source/Character` (`13` files, `2955` lines).

## Findings

| File & Line | Rule / Formula | Parity Reference | Result |
|---|---|---|---|
| `CharacterManager.cpp:312-342` | `GetEquipedBowType_Skill`: bow in left hand requires `ITEM_ARROWS` in right hand; crossbow in right hand requires `ITEM_BOLT` in left | Classic MU ammo check & Web `characterCalc.ts` | Same |
| `CharacterManager.cpp:344-390` | Magic skill adds `Damage + Damage/2`, set `AT_SET_OPTION_IMPROVE_MAGIC_POWER%` & mastery bonuses; Summoner `AT_SKILL_SUMMON_EXPLOSION..REQUIEM` use Curse Damage | Classic MU wizardry/curse formulas & Web `characterCalc.ts` | Same |
