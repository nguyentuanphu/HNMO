# Unit Audit: `client/src/source/MUHelper`

- **Unit**: `client/src/source/MUHelper`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `4` (`1,653` lines): `MuHelper.cpp` (`1,155L`), `MuHelper.h`, `MuHelperData.cpp` (`244L`), `MuHelperData.h`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| 257-byte MUHelper packet serialization | `MuHelperData.cpp:95-195` | Packs hunting/obtaining ranges (`4`-bit `ObtainRange`), `CastingBuffMin`, `HP/Heal/DrainLife/Party` thresholds divided by `10` (`4`-bit nibble), `Skill1/2` delay/condition/pre-condition/sub-condition bitfields (`>2 / >3 / >4 / >5` mobs), `12` extra item strings of max `15` bytes (`wcstombs`), and `byte 33` local flags (`UseSelfDefense`, `AutoAcceptFriend`, `AutoAcceptGuild`, `FallbackBasicAttack`) | Web `autoPlayConfig.ts:107-118` (`AUTO_PLAY_DATA_LENGTH = 257`) & Server `MuHelperConfiguration` | Same |
| MUHelper Zen cost guard & stage fee check | `MuHelper.cpp:30-35,70-76,807` | Checks `iGoldCost <= CharacterMachine->Gold` before keeping helper running; enforces MUHelper defaults (`HuntingRange`, `ObtainingRange`) | Server `MuHelperFeaturePlugIn` & `common-server-and-character-rules-s6-s9.md:20-21` | Same |
| Sub-byte 15-char truncation note | `MuHelperData.cpp:134-138` | Zeroes out `ExtraItems[i]` if `wcstombs` hits `>= 15` bytes (`(size_t)-1 || n == 15`), preventing unterminated overflow | Transport safety rule | Same |
