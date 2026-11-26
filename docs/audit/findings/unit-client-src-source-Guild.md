# Unit Audit: `client/src/source/Guild`

- **Unit**: `client/src/source/Guild`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `16` (`2,443` lines) including `GuildConstants.h`, `GuildInfoWindow.cpp`, `GuildMakeWindow.cpp`, `GuildRelationshipResultText.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — holds Guild member-limit formula and Guild creation / disband / alliance validation)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Guild member limit formula | `GuildInfoWindow.cpp:570-584` | If Guild Master is `CLASS_DARK_LORD`: `nCount = min(80, Level / 10 + Charisma / 10)`; other classes: `Level / 10` | `docs/audit/references/common-server-and-character-rules-s6-s9.md:39` (`MaxGuildMember = 80`) & `GuildServer` (`Level/10` vs DL `+Command/10` capped at 80) | Same |
| Guild creation name & emblem check | `GuildMakeWindow.cpp` | Requires alphanumeric guild name (`>= 4` chars) and non-empty `8x8` emblem | Server `GuildCreateAction` | Same |
| Guild relationship codes | `GuildConstants.h:26`, `GuildRelationshipResultText.cpp:15` | `UNION = 2` (Alliance), Hostility relationship code mapping | `GuildRelationshipRequestAction` (`Union = 1/2` protocol enum) | Same |
