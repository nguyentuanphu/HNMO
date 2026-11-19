# The 39 items without a picture: where their models are (2026-11-20, Sydney)

Job 7 of `docs/prompts/local-claude-overnight-2026-11-19.md`. Checked on the owner's PC against the base data
(`client/src/bin/Data`) and the owner's unpacked Season 9 data (`<Season 9 Data>`, 18,950 files). Only names, sizes
and versions are written here; no game file is in the repository.

How: `client/web/tools/make-item-icons.mjs` (commit e430d956) skips an item when `itemsWithModels()` doesn't list it:
it needs both a model entry (`Data/Items/Models/GroupNN_*.json`) and an English name (`Data/Items/GroupNN_*.json`).
For each of the 39 I read the definition and the model entry, parsed the model with the web game's own `parseBMD`
(`public/js/formats.js`), and searched both data sets for the model and its textures.

## Summary

| | items | what they need |
|---|---|---|
| A. stub definitions | 11 | nothing from Season 9: model and textures are in the base data |
| B. named items, no model entry | 28 | a model entry; the files are in the base data for 17 of them, in Season 9 only for 1 |
| of B, no candidate found | 10 | nothing found in either data set |

The Season 9 data does not say which model an item uses: that table is in the Season 9 client program (not read),
and the Season 9 `Local/` tables are encrypted. So every model for group B below is a **candidate found by name**,
with a confidence, to be checked by eye before it goes into a model entry.

## A. 11 stub definitions (model and textures already in the base data)

Their definitions hold only a number (`{"number":32}`; 12:73 also a size): no name, so the icon tool skips them as
"no model". Their model entries exist, and every model file and texture is in the base data (and identical in the
Season 9 data, same bytes):

| item | model (version) | textures (all present) |
|---|---|---|
| 5:32 | Item/Staff_33.bmd (0x0c) | agnecklace, soketel_soulbringer, bloodbone02, devileye |
| 7:53, 8:53, 9:53, 10:53, 11:53 | Player/HelmMale54, ArmorMale54, PantMale54, GloveMale54, BootMale54 (0x0c) | Item6001_* |
| 12:20 | Item/Gem14.bmd (0x0a) | pearl |
| 12:73, 12:74 | Item/s30_sphere_body04.bmd, s30_sphere_body05.bmd (0x0c) | sphere_item |
| 14:27 | Item/godesteel.bmd (0x0a) | godest01-03 |
| 15:25 | shared model `skillParchment` | |

These look like unused item numbers. If they are real items, they need a name (and size) in their definition, not a
model.

## B. 28 named items without a model entry

"in base" = the model is in the base data. "S9 only" = only in the Season 9 data (the pack takes it).

| item | name | candidate model | where | confidence | textures |
|---|---|---|---|---|---|
| 13:128 | Hawk Figurine | Item/hawkstatue.bmd (26,747 B, 0x0c) | in base | high | HawkStatue, HawkStatueB: in base |
| 13:134 | Worn Horseshoe | Item/horseshoe.bmd (7,919 B, 0x0c) | in base | high | punja, jing: in base |
| 13:129 | Goat Figurine | Item/sheepstatue.bmd (44,575 B, 0x0c) | in base | medium | SheepStatue, SheepStatueB: in base |
| 13:99 | Character Slot Key | Item/Ingameshop/key.bmd (33,338 B, 0x0c) | in base (Season 9's Item/key.bmd is the same bytes) | low | key: in base (Item/Ingameshop) |
| 13:117-120 | Minimum / Low / Medium / High Vitality Potion | Item/Ingameshop/FRpotionA-D.bmd (31,304 B each, 0x0c) | in base (same bytes as Season 9's Item/FRpotionA-D.bmd) | low | FRpotionA-D, FRpotion_R: in base (Item/Ingameshop) |
| 14:125 | Package Box | Item/Ingameshop/package_money_item.bmd (4,166 B, 0x0c) | in base (same bytes as Season 9's) | low | membership_item_red: in base (Item/Ingameshop) |
| 14:55-57 | Green / Red / Purple Chaos Box | Item/giftbox_g.bmd, giftbox_r.bmd (29,932 B), giftbox_bp.bmd (20,946 B), 0x0c | in base | low (the same three also fit 14:157-159) | giftbox_green / giftbox_red, leaf; whitedaybox03, whitedayribon02, whitedayrose, whitedaytrap: in base |
| 14:157-159 | Green / Red / Purple Box | same giftbox models as 14:55-57 | in base | low | as above |
| 14:151 | Lv. 1 Letter of Request | Item/questitem3rd00.bmd (6,401 B, 0x0a) | in base | low | gloryring: in base |
| 14:152 | Lv. 1 Completion Certificate | Item/questitem3rd01.bmd (8,146 B, 0x0a) | in base | low | darkstonered, darkstone8: in base |
| 14:153 | Stardust | Item/SpiritDust.bmd (40,233 B, **0x0e**) | **S9 only** | low | SpiritDust, SpiritDustAlpha: Season 9 only, in `Item/Texture/` |
| 12:26 | Gem of Secret | - | | none found | |
| 13:95 | Gladiator's Honor | - | | none found | |
| 13:96 | Seal of Strength | - | | none found | |
| 13:130-133 | Oak / Maple / Golden Oak / Golden Maple Charm | - | | none found | |
| 14:154 | Kalt Stone | - | | none found | |
| 14:155 | Tantalos's Armor | - | | none found | |
| 14:156 | Burnt Murderer's Club | - | | none found | |

Searched by name keywords in all 1,209 Season 9 `Item/` models (679 of them are not in the base data): honor,
gladi, seal, strength, slot, key, vital, potion, hawk, goat, oak, maple, charm, horse, shoe, chaos, box, package,
letter, request, certif, complet, star, dust, kalt, tantal, burnt, club, murder, statue, quest, gift.

## The pack builder

`client/web/tools/build-s9-pack.mjs --base-allowlist` (from #90) takes every Season 9-only file of the candidates:
checked in `<scratch>/s9-pack`: `Item/SpiritDust.bmd`, `Item/Texture/SpiritDust.OZJ`, `Item/Texture/SpiritDustAlpha.OZT`,
`Item/key.bmd`, `Item/FRpotionA-D.bmd`, `Item/package_money_item.bmd` are all there. No builder change is needed.

## What this means for the owner's choice ("take the missing models from the Season 9 pack")

The Season 9 data adds the model for only one of these items (14:153, if `SpiritDust` is right). For 17 more, a
candidate model is already in the base data, so they need a model entry, not new files. For 10, neither data set
has an obvious model. Before main Claude writes the model entries: draw each candidate once (the web game's item
renderer, as `make-item-icons.mjs` does) and keep the ones that look like the item; the low-confidence ones are only
name matches.

Not done: drawing the candidates (left for main Claude, who draws the pictures); checking the 10 "none found" items
against other names a model might have.
