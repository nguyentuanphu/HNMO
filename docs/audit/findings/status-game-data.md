# Game data agent (owner's 21 Nov answers), final

## Done
- Merged: #233 (socket shield protection level 5 = 20 % in Season 6; Season 9 needs 30 %), #234 (3rd wing luck 10 %), #237 (rank-2 class items = second class and up, 102 items), #240 (Crywolf warp level 300), #242 (lucky glove/boot speeds), #250 (shops: Silvia, Rhea, Marce, Natasha, Lumen +6 tickets), #251 (2nd wing +50 AG / +5 attack speed; Cape of Fighter own option set), #252 (harmony +14/+15), #253 (jewel drops by monster level band, configurable drop groups).
- #230 Blood Castle 15 min kept (owner).

## Draft, please review
- #257: fix-up for #250/#251/#253, which were merged before their test runs finished. The base branch has 4 failing Persistence.Initialization tests (merchant count 22 vs 23; drop group and Cape option names without resource texts). #257 adds the texts in en/de/vi/zh-CN and the count 23. After: 973 passed of 985 (12 skipped), HNMO.Tests 3712 of 3714 (2 skipped). #252's full run was never completed before its merge; #257's full run covers it.
- #258: Summoner set 44 is the Eternal Wing set (both clients' item data show "Eternal Wing"); set bonuses renamed "Eternal Defense Bonus ...". Its 4 failures are the base failures that #257 fixes; merge #257 first.

## Not done, with reasons
- Area-skill distances: HNMO uses the skill range as the area radius; copying the suites' cast distance (0 for Hellfire, Inferno) would make them hit nothing. Needs a code change. #254 (not from this agent) may cover it; check.
- Nova AG: already the same in effect (Nova start costs 45 AG).
- Socket weapon mix: the suites give only its rate (40 %) and cost (1,000,000 zen); the recipe is in neither the suite files nor the clients' mix data.
- Shops for Arena Guard (239), Jin (583/604), Moss (492): HNMO has no such NPCs (Jin stands on maps 91/92, no Season 6 data); Moss sells gamble items 13-71..75 that HNMO has no definitions for. Natasha's spawn in Devias not checked.
- Owner question: Horn of Fenrir (13,37) is rank 2 in the suites too, but its classes come from the pet data; left out of #237.
- Noted, not changed: item 13-50 is "Scroll of Blood" (2x2) in HNMO; the suites have the Illusion Sorcerer Covenant there.

## Kept HNMO (owner, 21 Nov)
Event ticket drops 1 %, Archangel weapons (Season 9 values), base HP/mana of DW/DK/Summoner/RF, item prices, item trade/drop/store/sell/repair flags, skill class lists. +12/+14 chaos mix rates kept (20 Nov; #217 closed).
