# Unit Audit: `client/src/source/GameLogic/Party`

- **Unit**: `client/src/source/GameLogic/Party`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `2` (`46` lines): `PartyMemberSkill.cpp`, `PartyMemberSkill.h`
- **Status**: `Checked` (reclassified from `"No game rules"` — defines party-member-targetable buff skills)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Party-member clickable skill whitelist | `PartyMemberSkill.cpp:7-34` | Whitelists Elf Heal / Greater Defense / Greater Damage (+ strengthened variants), DW Soul Barrier (+ strengthened), Summoner Damage Reflection, etc. when clicking a party bar member frame | Server buff target validation (`TargetType` / party buff skills) | Same |
