# Item serials against duplicates, and market access fee (plan, 2026-11-20, Sydney)

## The owner's decisions (pop-up, 2026-11-20 morning)

- **Anti-dupe:** every item gets an origin serial when it is made. The serial follows the item through trades,
  the market, escrow, vaults and moves. A scan every hour finds two live items with one serial: both are frozen
  (they can't be traded, sold, dropped or stored) and the admin gets an alert to decide.
- **Market:**
  - a one-off access fee of **1,000 WCoin (C)** per account unlocks buying and selling;
  - each sale pays a **5% tax** (in place of today's 5% fee);
  - the 10,000 zen listing fee stays;
  - all three are set in the admin panel.

## Notes

- **Serials today:** every item row in the database has its own id (GUID), but a duplicated item gets a new id,
  so ids can't find duplicates. The serial is made once, when the item is first made (drop, shop, crafting, cash
  shop, gift, event reward), and copied on any move that keeps the item.
- **Where serials can legitimately be equal:** none. Splitting a stack makes a new serial for the split part, and
  joining stacks keeps the target's serial.
- **The scan:** reads every item's serial in the database (an index on it) and the market and escrow storages.
  An alert lists both owners and the item's history.
- **Market access:** paid once per account, in the game (F6) or on the website, with a receipt. It is checked by
  the market on list and buy (game and website).
