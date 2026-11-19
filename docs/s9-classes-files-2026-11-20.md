# Season 9's new classes: which files they use (2026-11-20, Sydney)

Job 6 of `docs/prompts/local-claude-overnight-2026-11-19.md`, step 6.1 of `docs/plans/seasons-2026-11-19.md`.
Checked on the owner's PC: the owner's unpacked Season 9 data (`<Season 9 Data>`, 18,950 files) against the base
data (`client/src/bin/Data`). Names, counts and sizes only; no game file is in the repository.

## Result: this Season 9 data has no new character class

The web game draws seven classes (`client/web/src/net/session.ts`, `CLASSES`: Dark Wizard, Dark Knight, Fairy Elf,
Magic Gladiator, Dark Lord, Summoner, Rage Fighter). The Season 9 data has the same seven, no more:

- **Class model sets** (`Player/`): the per-class sets are numbered the same in both data sets:
  `SkinClass` 106, 301-306, 406, 506, 606, 706; `Skinclass` 106, 107, 206; `ArmorClass` and `HelmClass` 01-07, 201-203,
  206, 301-307 (the same for Pant, Glove, Boot). No class number is new.
- **`Player/`**: 1,030 files in Season 9, **none** missing from the base data; 1,028 are the same bytes. Two differ:
  `player.bmd` (3,472,315 B against 3,324,834 B) and `skin_warrior_01.OZJ` (70,021 B against 72,632 B).
- **No class-named files anywhere**: searched the whole Season 9 data for the next class's usual markers (its name
  in English, "lance", "gl_"); the only hits are a monster's and an effect's lance.

So step 6 ("make the new classes work from the Season 9 files") has nothing to take from this data: a class added in
a later episode is not in it. If the owner has a newer Season 9 client, its data would need the same check.

## What Season 9 changes for the existing classes

| | Season 9 | base | notes |
|---|---|---|---|
| `Player/player.bmd` | version 0x0e, 293 actions, 60 bones, 31 locked | version 0x0c, 284 actions, 60 bones, 33 locked | 9 new actions (284-292; keys 5, 5, 3, 3, 3, 3, 4, 7, 46). The web game reads 0x0e (`modulusCipher.ts`). Which skills or emotes use them is not in the data. |
| `Player/skin_warrior_01.OZJ` | 70,021 B | 72,632 B | a texture redone |
| `Skill/` | 403 files, 37 only in Season 9 (18 .bmd, 16 .ozj, 3 .ozt; 2,126,102 B) | 366 | the new skills' models and textures |
| `Effect/` | 399 files, 76 only in Season 9 (18 .bmd, 55 .ozj, 3 .ozt; 3,338,664 B) | 323 | new effects |

Every Season 9-only file of `Skill/` and `Effect/` is in the pack `build-s9-pack.mjs --base-allowlist` makes (checked
in `<scratch>/s9-pack`: 0 missing), and so are `player.bmd` and `skin_warrior_01.OZJ` (they differ from the base).
No pack builder change is needed.

## For step 6.2

- Nothing to add for a new class from this data.
- The 9 new player actions and the 37 + 18 new skill and effect models are what a Season 9 skill set would use; the
  skill-to-action and skill-to-effect tables are in the Season 9 client program, not in its data (its `Local/`
  tables are encrypted), so they can't be listed from the files alone.

Not checked: the sounds (no class-specific sound folder exists in either data set).
