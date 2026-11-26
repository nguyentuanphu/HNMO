# Unit Audit: `client/src/source/Network/Server (files 1)`

- **Unit**: `client/src/source/Network/Server (files 1)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `25` (`2,569` lines) including `SocketSystem.cpp`, `CSMapServer.cpp`, `GuildRelationshipCodes.h`, `IllusionTempleResult.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — `SocketSystem.cpp` holds socket item eligibility, seed category mapping, active-option durability check, and bonus item-value/option-value formulas)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Socket item whitelist | `SocketSystem.cpp:56-102` | `IsSocketItem` explicitly lists all socket weapons, shields, and armor sets (Titan, Brave, Destroy, Phantom, Seraphim, Faith, Paewang, Hades, etc.) | Server `ItemDefinitions` `MaximumSockets > 0` | Same (note whitelist must stay synced when adding custom socket sets) |
| Socket option active durability gate | `SocketSystem.cpp:66-69` | `AreSocketOptionsActive`: requires `IsSocketItem(pItem) && pItem->Durability != 0` | Server item durability `> 0` disables options at `0` | Same |
| Seed Sphere ID -> category mapping (`0..40`) | `SocketSystem.cpp:112-144,160-168` | Maps Fire (`0..9`), Water (`10..15`), Ice (`16..20`), Wind (`21..28`), Lightning (`29..33`), Ground (`34..40`) to categories `1..6` and `ITEM_SEED_SPHERE_FIRE_1 + (sphereLv - 1)*6 + cat` | Server `SocketSystem` seed & sphere definitions (`socket-options-and-pets-s6-s9.md`) | Same |
| Socket bonus NPC/item value formula | `SocketSystem.cpp:146-173` | `iGoldResult += iOrgGold * (SocketCount * 0.8f)` plus `ItemValue` of each mounted Seed Sphere | Server `ItemPriceCalculator` socket multiplier (`0.8 * sockets + mounted sphere prices`) | Same |
| Socket option value scaling types (`1..5`) | `SocketSystem.cpp:175-218` | `1`: flat integer, `2`: flat `%`, `3`: `TotalLevel / fOptionValue`, `4`: `MaxLife / fOptionValue`, `5`: `MaxMana / fOptionValue` | Server socket option value scaling rules | Same |
| Map-server / guild relation / IT codecs | Remaining files (`CSMapServer.cpp`, etc.) | Sub-server address switching, guild relationship protocol bytes, Illusion Temple result unpacking | Client network plumbing | No game rules |
