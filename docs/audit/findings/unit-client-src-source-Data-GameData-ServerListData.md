# Unit: client/src/source/Data/GameData/ServerListData

Checked against: ConnectServer load percentage protocol.

Scope: `client/src/source/Data/GameData/ServerListData` (`27` files, `2495` lines).

## Findings

| File & Line | Rule / Constant | Role & Value | Result |
|---|---|---|---|
| `LoadState.h:8-9`, `LoadState.cpp:7-13` | `FullLoadPercent = 100`, `OverloadedLoadPercent = 128` map server load bar states | Client server-list threshold | Same as classic |
