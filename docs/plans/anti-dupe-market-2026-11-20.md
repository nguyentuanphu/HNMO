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

## Done (2026-11-20, Sydney)

- **Serials:** `Item.Serial` (database column `data."Item"."Serial"` with an index, migration `AddItemSerial`), copied by
  every copy of an item (`AssignValues` and the generated clones). Given once: by every scan in the database (player
  items only; NPC shop stock and quest reward templates never get one, because buying copies them), and to the bag
  and vault of a character entering the game.
- **Scan:** `DuplicateItemsPlugIn` (on by default; admin panel Plugins: scan every 60 minutes, freeze on/off). It gives
  the missing serials, finds serials held by more than one item (`PostgresItemSerialDatabase`) and records a case in the
  state file. A case stays frozen until decided; a decided case isn't opened again.
- **Freeze:** a frozen item can't be traded, sold, dropped, put in a store, listed, put in escrow, mixed or moved into
  the vault (the account safety's item lock checks, and the item move).
- **Admin:** page "Duplicate items" (`/duplicate-items`, players group): the cases with both owners and where the items
  are, "Scan now", "Mark decided" with a note; times in Sydney time.
- **Checked on PostgreSQL:** the migration ran at the server's start; the first scan gave 3,653 player items a serial
  (963 shop and quest template items none); a copied serial was found by "Scan now", listing the frozen item on the
  market was refused in the game with the EN notice, and after "Mark decided" the same listing went through.
- **Gap:** an item made during a session and copied in the same session (before any scan or the next login) has no
  serial yet, so its copy isn't found. Copies of items older than the last scan or login are.

## Market access fee and tax: done (2026-11-20, Sydney)

- `MarketConfiguration.AccessFee` (1,000) and `AccessFeeCoinType` (WCoinC), set in the admin panel; 0 makes the
  market free. Paid once per account with the market window's "Unlock market" button (packet action `PayAccess` = 6),
  with a coin receipt "Market access"; the paid accounts are kept in the market's state (`AccessPaid`).
- Listing and buying in the game are refused until it's paid; the website's buy answers `access` (pay in the game).
- The 5% sale tax is the existing `FeePercent` (5), now called a tax in the window and the guide; the 10,000 zen listing
  fee stays.
- Checked live on PostgreSQL: test2 (1,500 WCoinC) was refused when listing, unlocked the market (500 left), then the
  same listing went through. `MarketControllerTests` checks the website refusal.
