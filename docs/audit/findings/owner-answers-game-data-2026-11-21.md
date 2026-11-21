# Owner's answers on game data (21 Nov 2026)

The questions are in ledger-game-data.md. "the Season 6 suite" means the Season 6 suite folder (Season 6) and "the Season 9 suite" means the Season 9 suite folder (Season 9 ep2). In the repo, write "the Season 6/9 server suites".

## Do (one draft PR each)
- Class rank 2 items (Q1): follow the suites. Rank 2 means second class and up.
- Blood Castle play time 15 min: keep #230. "Keep event timetables" meant start times only.
- 3rd wing mix luck: 10 %, as the Season 6 suite.
- Crywolf warp level 300, as the Season 6 suite. Keep Loren Market.
- Skills: area-skill distances and Nova AG as the suites. KEEP HNMO's class lists (Abolish Magic, Lance).
- NPC shops: follow the Season 6 suite. That means:
  - the Elbeland NPCs 415/416/417
  - add the missing shops: Arena Guard, Natasha, the Jin NPCs, Moss
  - add the Illusion Temple +6 tickets at Lumen
- 2nd wings: add the suites' +AG and +attack speed options. KEEP HNMO's prices (excellent +40 %, Jewel of Chaos 810,000, Loch's Feather / Crest 180,000).
- Item flags: KEEP HNMO's trade/drop/store/sell/repair flags. Only add the missing attack/walk speed to gloves and boots 62-72.
- Harmony options: add the +14/+15 values.
- Socket weapon mix (40 %, 1 M zen): add it, as the Season 6 suite.
- Jewel drops by monster level: follow the suites' per-monster-level jewel table instead of one group at 0.1 %. This is a big change to the game economy.
  - Keep it configurable.
  - Put the before/after jewel drop rates per level band in the PR body.
- Item set 44: look at the item data of the web client and the desktop client first.
  - If they show "Eternal Wing", follow the suites.
  - If they show "Lilium", keep HNMO and report it.
- Socket "shield protection" level 5: the two seasons differ. Season 6 data gets 20 (the Season 6 suite); Season 9 keeps 30 (the Season 9 suite). HNMO has no Season 9 data set yet, so change the S6 value to 20 and note in the PR that S9 needs 30 when an S9 data set exists.

## Keep HNMO (no PR, evidence "owner kept HNMO 21 Nov")
- Event ticket drops 1 %.
- Archangel weapons (S9 values).
- Base HP/mana of DW, DK, Summoner, RF.
- Item prices.
- Item flags (except the speeds).
- Skill class lists.

## Later, for main
- A separate Season 9 data set (the Season 9 suite changes 210 shared items, has 281 S9-only items and new socket values).
