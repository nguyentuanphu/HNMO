# Unit: server/src/GameServer/RemoteView/CashShop

Checked against: Season 6/9 client CashShop storage grid size.

Scope: `server/src/GameServer/RemoteView/CashShop` (`1` file, `161` lines).

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `CashShopViewPlugIn.cs:28` | `ItemsPerStoragePage = 9` items per storage page response | Client 3x3 CashShop storage grid (`9` items/page) | Same |
