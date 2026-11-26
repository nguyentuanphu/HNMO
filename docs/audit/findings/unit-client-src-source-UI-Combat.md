# Unit Audit: `client/src/source/UI/Combat`

- **Unit**: `client/src/source/UI/Combat`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `35` (`5,507` lines) including `CastleWindow.cpp`, `GuardWindow.cpp`, `DuelWindow.cpp`, `DuelWatchWindow.cpp`, `SiegeWarBase.cpp`, `SiegeWarfare.cpp`, `SiegeWarCommander.cpp`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Castle Statue recovery upgrade costs | `CastleWindow.cpp:420-438` | Recover level `0`: `3` Guardian Jewels + `5,000,000` Zen; level `1`: `5` + `5,000,000`; level `2+`: `7` + `5,000,000` | Server `CastleSiege` Senior / NPC Senatus upgrade rules | Same |
| Castle Siege tax rate confirm & treasury withdraw cap | `CastleWindow.cpp:441-470` | Prompts Chaos Machine & Normal NPC tax rate changes (`GetChaosTaxRate` / `GetNormalTaxRate`); displays `Maximum 15,000,000 Zen` withdraw prompt cap | Server `CastleSiege` tax/withdraw rules (`castle-siege-s6-s9.md`) | Same |
| Sign of Lord filter (`ITEM_RENA` level `3`) | `GuardWindow.cpp:44` | Recognizes Sign of Lord (`item->Type == ITEM_RENA && item->Level == 3`) for Castle Siege guild registration | Web `castleSiegeWindows.ts:103` & Server `CastleSiege` | Same |
| Dark Lord Guild Siege alliance commander flag | `SiegeWarBase.cpp:153`, `SiegeWarCommander.cpp` | Evaluates Master / Dark Lord alliance command permissions & minimap tactical pings | Web `fieldEventWindows.ts:306-308` | Same |
| Duel spectator & colosseum UI | `DuelWindow.cpp`, `DuelWatchWindow.cpp`, `DuelWatchMainFrameWindow.cpp` | Displays 4 Duel Colosseum rooms & spectator HP/SD gauge ratios | Web `duelWindows.ts` | Same |
