# Unit: client/src/source/GameLogic/Items (files 1)

Checked against: Web `mixRecipes.ts`, Server `MixHandler` & `LahapJewelMix`.

Scope: `client/src/source/GameLogic/Items (files 1)` (`34` files, `5925` lines).

## Findings

| File & Line | Rule / Constant | Parity Reference | Result |
|---|---|---|---|
| `MixMgr.cpp:112-199` | Parses `Mix.bmd`, flags `RCP_SP_EXCELLENT / ADD380ITEM (>= Lv 380) / SETITEM`, scales `AT_LIFE_REGENERATION` opt `* 4` | Web `mixRecipes.ts` | Same |
| `CComGem.cpp:172-235` | Lahap Jewel Mix/Unmix validates target bundle count (`100%` success rate) in inventory | Classic Lahap jewel combine | Same |
