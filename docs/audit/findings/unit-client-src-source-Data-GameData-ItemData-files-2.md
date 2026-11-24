# Unit: client/src/source/Data/GameData/ItemData (files 2)

Checked against: Server `ItemOptionDefinition` & Web `itemValue.ts`.

Scope: `client/src/source/Data/GameData/ItemData (files 2)` (`37` files, `5893` lines).

## Findings

| File & Line | Rule / Constant | Parity Reference | Result |
|---|---|---|---|
| `ItemOptionEffects.cpp:26-35` | Maps `criticalDamageChance -> AT_LUCK`, `healthRecoveryMultiplier -> AT_LIFE_REGENERATION`, `maximumMana -> AT_IMPROVE_MAX_MANA`, `maximumAbility -> AT_IMPROVE_MAX_AG` | Client/Server option effect parity | Same |
