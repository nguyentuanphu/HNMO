# Unit Audit: `client/src/source/Data/GameData/ServerListData`

- **Unit**: `client/src/source/Data/GameData/ServerListData`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `27` (`2,495` lines) including `LoadState.cpp`, `LoadState.h`, `LoadBar.cpp`, `ChannelNameBuilder.cpp`, `ServerListJson.cpp`, `VipType.h`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Server population load status thresholds | `LoadState.cpp:7-14`, `LoadState.h:8-9` | `GetLoadState(percent)`: returns `LoadState::Overloaded` when `percent >= OverloadedLoadPercent`, `LoadState::Full` when `>= FullLoadPercent`, else `LoadState::Open` | `ConnectServer` server load % buckets | Same |
| Channel naming, PvP/VIP badges & bar theme | `ChannelNameBuilder.cpp`, `ServerListJson.cpp`, `VipType.h`, `ButtonColors.cpp` | Formats Non-PvP / Castle Siege / VIP sub-server labels and load bar fill percentages from server list JSON | Server list presentation | No game rules |
