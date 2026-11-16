# Offline Trade (`/offtrade`)

The **Offline Trade** plug-in (`OfflineTradePlugIn` and `/offtrade` command) lets players leave their personal shop open in town while logged out, selling items for Zen or any enabled server currency (`WCoinC`, `WCoinP`, `GoblinPoints`, or jewels via `/shopprice`).

## Key Options (`/off-trade`)

- **Allow Offline Trade**: OFF by default until the administrator activates the plug-in.
- **Safe zone only**: Requires the character to stand inside a safe zone before going offline.
- **Allow coins & jewels**: Controls whether stores priced in account coins or jewels may go offline, or restricts off-trade to Zen-only stores.
- **Stop when sold out**: Saves proceeds and removes the offline shop clone automatically once the last item sells.
- **Max hours / Open fee / Min level / Allowed maps**: Optional duration cap, Zen opening fee, level gate, and map number whitelist.
- **Live active shops list**: Shows currently active off-trade characters, store titles, coordinates, item counts, and Sydney-time timestamps, with a **Stop** action.
