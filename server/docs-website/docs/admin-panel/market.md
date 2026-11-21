# Item market

The **Item market** lets players buy and sell items safely using in-game escrow, both in the web game (`F6` or talking to **Market Julia** NPC `547`) and on the website.

## How it works

1. Turn on **Enable Item Market** on `/market` (off by default).
2. Choose allowed currencies (`Zen`, `WCoinC`, `WCoinP`, `GoblinPoints`), the percentage **Sale fee**, the upfront **Listing Zen fee**, active listing limit, expiry duration, and **Banned items** (`group:number`, one per line).
3. When a player lists a bag item in-game, the item is removed from their inventory immediately into escrow so it cannot be duplicated.
4. Concurrent purchases claim listings atomically; if the buyer's bag is full (or they bought on the website), the item waits in their escrow mailbox to be claimed with `F6`.
5. Administrators can cancel any active listing in `/market` to return the item to the seller's escrow mailbox and inspect the full Sydney-time audit log.
