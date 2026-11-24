# Unit: client/src/source/MUHelper

Checked against: Web `autoPlay.ts` / `autoPlayConfig.ts` & Server `MuHelperConfigurationData`.

Scope: `client/src/source/MUHelper` (`4` files, `1653` lines).

## Findings

| File & Line | Rule / Constant | Parity Reference | Result |
|---|---|---|---|
| `MuHelper.cpp:30-35` | `MAX_ACTIONABLE_DISTANCE = 10`, `DEFAULT_DURABILITY_THRESHOLD = 50`, `PICKUP_RETRY_DELAY = 1s`, `PICKUP_REQUEST_RETRY_DELAY = 3s`, `MAX_PICKUP_REQUESTS = 3`, `TARGET_RETRY_DELAY = 2s` | Web `autoPlay.ts:16-22` | Same |
| `MuHelperData.cpp:97-192` | Packs/unpacks MUHelper settings to server `MuHelperSaveDataRequest` bytes | Web `autoPlayConfig.ts` | Same |
