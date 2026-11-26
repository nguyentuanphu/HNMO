# Unit Audit: `client/src/source/UI/Events (files 2)`

- **Unit**: `client/src/source/UI/Events (files 2)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `32` (`4,058` lines) including `EnterDevilSquare.cpp`, `ExchangeLuckyCoin.cpp`, `GateSwitchWindow.cpp`, `GoldBowmanWindow.cpp`, `KanturuEvent.cpp`, `RegistrationLuckyCoin.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — `EnterDevilSquare.cpp` and `ExchangeLuckyCoin.cpp` encode event level-band eligibility and Lucky Coin exchange tiers)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Devil Square entry band selection | `EnterDevilSquare.cpp:129-175` | Master characters select `MAX_ENTER_GRADE - 1` (DS7); MG/DL/RF use offset `iVal = 7` in `m_iDevilSquareLimitLevel`; non-Master selects matching `(min, max)` band index | Server `DevilSquare` level brackets & MG/DL/RF discount | Same |
| Lucky Coin exchange amounts | `ExchangeLuckyCoin.cpp:83,115-116` | Sends `SendLuckyCoinExchangeRequest(10 * (pressed + 1))` for `10`, `20`, or `30` Lucky Coins | Server `LuckyCoin` NPC exchange (`10/20/30` tiers) | Same |
| Other event UI views & updaters | Remaining files (`GateSwitchWindow`, `KanturuEvent`, `RegistrationLuckyCoin`, etc.) | RmlUi event entry views, timers, and S->C state updaters | UI presentation | No game rules |
