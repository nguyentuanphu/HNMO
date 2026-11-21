# Season 9's new character classes: what the owner's data has (2026-11-20, Sydney)

Job 11 of `docs/prompts/local-claude-overnight-2026-11-19.md` (main Claude's message of 07:15). Checked on the
owner's PC: the owner's Season 9 client data (`<Season 9 Data>`, 18,965 files) and the Season 9 server files
(`<s9 server files>`, the ones the importer read in Job 8). Names, counts and sizes only; no game file is in the
repository. Follows Job 6 (`docs/s9-classes-files-2026-11-20.md`).

## Result: no new playable class

The data has the seven classes the web game draws (Dark Wizard, Dark Knight, Fairy Elf, Magic Gladiator, Dark Lord,
Summoner, Rage Fighter) and no eighth one. The next class in the series (Grow Lancer) is named only in two server
settings, with nothing to play it with:

| where | what it holds for an eighth class |
|---|---|
| client data `Player/` (871 files at the top, 1,030 with sub-folders) | nothing new: the class model sets are numbered as in the base data (Job 6) |
| client data, any folder, names with "lance", "grow" or "gl_" | 10 hits, none a class: a monster's lance (6 textures), an effect's lance (2 models, 1 texture), one ambient sound |
| server: the data server's settings (2 files) | a commented setting "minimum level of a regular character to unlock Grow Lancer creation" |
| server: the class quest table | a `GrowLancer` column; 7 of its 22 rows set it |
| server: the item list (1,284 items) | class columns for the seven classes only; no `GrowLancer` column |
| server: the skill list (403 skills) | no `GrowLancer` column |

So the server program knows the class (a setting and quest rows), but this Season 9 has no items, skills, armour,
weapons, animations, effects, textures or sounds for it.

## Per new class (asked for each)

| | Grow Lancer |
|---|---|
| class name and number | named in the server settings only; no class number in the client data or the item and skill lists |
| armour sets | none (no `Player/` set beyond the seven classes') |
| weapons | none (no item usable by it) |
| animation set | none new; `Player/player.bmd` has 293 actions against the base's 284 (9 new, 284-292), not tied to a class in the data |
| skills | none (no skill for it in the skill list) |
| effect models and textures | none named for it; Season 9's 37 new `Skill/` and 76 new `Effect/` files belong to the seven classes' new skills and to monsters (Job 6) |
| sounds | none (no class sound folder in either data set) |

## Does `build-s9-pack` take them?

Nothing to take for a new class. The pack builder's folders (`Monster`, `Item`, `Skill`, `Effect`, `Player`, `NPC`,
`Sound`, `Music`, `World*`, `Object*`) cover every file the seven classes use; Job 6 checked that every Season
9-only file of `Skill/` and `Effect/`, and `player.bmd` and `skin_warrior_01.OZJ`, are in the pack.

## For the owner

To play an eighth class, a newer client's data would be needed (its `Player/` sets, items, skills and effects). This
Season 9 data doesn't have one; the same checks would run on a newer data folder.
