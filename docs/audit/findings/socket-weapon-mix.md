# Socket weapon mix (40 %, 1,000,000 zen)

Findings from comparing HNMO with the Season 6 and Season 9 server suites and the Season 9 client data.

## Summary

- **Found in the Season 9 suite and Season 9 client data (`Mix.bmd`):** 12 Chaos Machine recipes (mix ID `57`, title text `3758` `"Item Refinement"`, `MIXTYPE_GOBLIN_NORMAL` indices `113–124`), each with a **40 %** maximum success rate and a **1,000,000 zen** cost.
- **Season 6 suite:** `ChaosBox.ini` (`[SocketWeapon]`) and `AppointItemDrop.xml` (`DropGroup` `4–12`) carry the same rate (`40`), zen (`1000000`), and material drop rows, **but** none of the 21 material items (`14:180–188`, `14:191–202`) or 12 Level-400 socket weapons exist in the Season 6 `ItemList.xml` (or in HNMO's Season 6 database / `mix.bmd`).
- **Blocker before adding crafting data:** All 33 items (21 ingredients + 12 result weapons) are part of the **281 Season 9-only items** that HNMO does not have yet. They must be added in the Season 9 item data PR before the crafting initializer, mandatory update plug-in, and tests can reference them.

## Suite files

- **Season 6 server suite:**
  - `ChaosBox.ini` (`[SocketWeapon]`: `SocketWeaponMixMaxRate = 40`, `SocketWeaponMixRequireMoney = 1000000`)
  - `AppointItemDrop.xml` (`DropGroup` `4–12` for Raklion monsters `454–458`, `562–565`, referencing `14:180–188` and `14:191–202`)
  - `ItemList.xml` (lacks `14:180–202` and the 12 Level-400 socket weapons)
- **Season 9 server suite:**
  - `ChaosBox.ini` (`[SocketWeapon]`: `SocketWeaponMixMaxRate = 40`, `SocketWeaponMixRequireMoney = 1000000`)
  - `VipSettings.xml` (`<Socket ... SocketWeapon="40" />`)
  - `ItemList.xml` (defines `14:180–188`, `14:191–202`, and the 12 result weapons with `Drop="0"`, `DropLevel="149"`, `ReqLevel="400"`, `Type="2"`)
  - `AppointItemDrop.xml` (`DropGroup` `4–12` for Raklion monsters `454–458`, `562–565`)
- **Season 9 client data:**
  - `Data/Local/Mix.bmd` (recipes `113–124` in `MIXTYPE_GOBLIN_NORMAL`, `m_iMixID = 57`, `m_iMixName = [3758, 0, 0]`)
  - `Data/Local/Eng/Text_eng.bmd` (`3758` = `"Item Refinement"`)

## Shared mix rules (`Mix.bmd`, ID `57`)

- **NPC:** Chaos Goblin (`MIXTYPE_GOBLIN_NORMAL`)
- **Zen:** `1,000,000` (`m_bRequiredZenType = 'A'`, `m_dwRequiredZen = 1000000`)
- **Success rate:** `INT(sum of mix item values / 360000)`, capped at **`40 %`** (`m_iSuccessRate = 40`)
- **Charms:** `m_bCharmOption = 'A'` (Talisman of Luck allowed), `m_bChaosCharmOption = 'U'` (Talisman of Chaos Assembly disallowed)
- **Common equipment ingredient in all 12 recipes:**
  - **3 ×** Socket equipment items (groups `0–11`, i.e., item type `0..6143`), level **`+7` or higher**, additional option **`+4` or higher**, socket item flag (`RCP_SP_SOCKETITEM`)

## The 12 recipes

Each recipe uses the **3 ×** Socket equipment (`+7+`, `+4+` option) above plus **1 ×** Combination Note, **2 ×** normal Raklion material, and **1 ×** Dark Raklion material:

| # | Mix.bmd idx | Combination Note (`14:191–202`) | Qty 2 Material (`14:180–184`) | Qty 1 Material (`14:185–188`) | Result Socket Weapon (`ItemList.xml`, Lv.400) |
|---|---|---|---|---|---|
| 1 | 113 | `14:191` Sonic Combination Note | `14:180` Ice Walker's Mane | `14:188` Dark Iron Knight's Sword | `0:29` Sonic Blade |
| 2 | 114 | `14:192` Cyclone Combination Note | `14:181` Mammoth's Bundle of Fur | `14:185` Dark Mammoth's Horn | `0:36` Cyclone Sword |
| 3 | 115 | `14:193` Blast Combination Note | `14:182` Giant's Icicle | `14:188` Dark Iron Knight's Sword | `0:37` Blast Break |
| 4 | 116 | `14:194` Magma Combination Note | `14:183` Coolutin's Poison | `14:185` Dark Mammoth's Horn | `3:12` Magmus Peer |
| 5 | 117 | `14:195` Horn Combination Note | `14:182` Giant's Icicle | `14:186` Dark Giant's Axe | `2:20` Horn of Steal |
| 6 | 118 | `14:196` Angelic Combination Note | `14:180` Ice Walker's Mane | `14:185` Dark Mammoth's Horn | `4:25` Angelic Bow |
| 7 | 119 | `14:197` Devil Cross Combination Note | `14:184` Iron Knight's Heart | `14:187` Dark Coolutin's Blood | `4:26` Devil Crossbow |
| 8 | 120 | `14:198` Miracle Combination Note | `14:182` Giant's Icicle | `14:185` Dark Mammoth's Horn | `5:35` Miracle Staff |
| 9 | 121 | `14:199` Spite Combination Note | `14:184` Iron Knight's Heart | `14:186` Dark Giant's Axe | `5:37` Spite Staff |
| 10 | 122 | `14:200` Asura Combination Note | `14:184` Iron Knight's Heart | `14:188` Dark Iron Knight's Sword | `0:30` Asura |
| 11 | 123 | `14:201` Thunder Combination Note | `14:181` Mammoth's Bundle of Fur | `14:187` Dark Coolutin's Blood | `2:19` Thunderbolt |
| 12 | 124 | `14:202` Soul Combination Note | `14:184` Iron Knight's Heart | `14:185` Dark Mammoth's Horn | `5:32` Summon Spirit Stick |

*(Note: `14:203` Greater Combination Note and `0:38` Frick Large Glove are commented out in the Season 9 suite `ItemList.xml` and `AppointItemDrop.xml` and have no recipe in `Mix.bmd`.)*

## Material drops (`AppointItemDrop.xml`, Raklion)

- **Ice Walker (`454`):** `14:180` Ice Walker's Mane (`1450/10,000,000`), Combination Notes `14:191–202` (`360/10,000,000` each)
- **Giant Mammoth (`455`):** `14:181` Mammoth's Bundle of Fur (`1630/10,000,000`), Notes `14:191–202` (`450/10,000,000` each)
- **Ice Giant (`456`):** `14:182` Giant's Icicle (`2040/10,000,000`), Notes `14:191–202` (`540/10,000,000` each)
- **Coolutin (`457`):** `14:183` Coolutin's Poison (`720/10,000,000`), Notes `14:191–202` (`630/10,000,000` each)
- **Iron Knight (`458`):** `14:184` Iron Knight's Heart (`3450/10,000,000`), Notes `14:191–202` (`720/10,000,000` each)
- **Dark Mammoth (`562`):** `14:185` Dark Mammoth's Horn (`1810/10,000,000`), Notes `14:191–202` (`680/10,000,000` each)
- **Dark Giant (`563`):** `14:186` Dark Giant's Axe (`900/10,000,000`), Notes `14:191–202` (`770/10,000,000` each)
- **Dark Coolutin (`564`):** `14:187` Dark Coolutin's Blood (`650/10,000,000`), Notes `14:191–202` (`810/10,000,000` each)
- **Dark Iron Knight (`565`):** `14:188` Dark Iron Knight's Sword (`1180/10,000,000`), Notes `14:191–202` (`860/10,000,000` each)
