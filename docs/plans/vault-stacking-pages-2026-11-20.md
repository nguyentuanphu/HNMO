# Vault pages and bigger item stacks (plan, 2026-11-20, Sydney)

## The owner's decisions (pop-up, 2026-11-20 ~06:55 Sydney)

- **Vault pages:**
  - **2 pages per account, free** (owner, 2026-11-20 ~08:30; earlier 3);
  - VIP +1;
  - more pages bought with coins;
  - all set in the admin panel;
  - page buttons in the vault window, and `/vault N` keeps working.
- **What stacks:**
  - potions, event tickets and keys stack **free**;
  - jewels, boxes and quest items, and crafting materials stack once the account **unlocks them with coins**.
- **Stack size:** up to **255** of one item in one slot (the item count is one byte). Changeable per kind in the
  admin panel.
- **Where:** the vault **and the bag**.

Second pop-up (07:10 Sydney):
- **Unlocks:** each kind is unlocked separately (jewels; boxes and quest items; crafting materials).
- **Default prices:** a stack unlock costs 500, an extra vault page 200, both paid in **WCoin (C)**; changeable in
  the admin panel.
- **Desktop client:** **web only** for now.

## How it works today

- An item stacks when it isn't wearable and its definition's durability is above 1 (`ItemExtensions.IsStackable`).
- The count is the item's durability; the limit is the definition's durability (`CanCompletelyStackOn`,
  `CanPartiallyStackOn`).
- Jewels have durability 1, so they never stack.
- Multi-vault pages exist (`MultiVaultPlugIn`, 2026-11-20: each page is its own item storage). The defaults are
  1 page, VIP +1, buy more; switching is only by `/vault N`.

## Steps (each tested, then committed)

1. **Server stack rules.** A stack policy (plug-in configuration, admin page):
   - kinds of items by group and number ranges;
   - each kind: free or coin-unlocked, and its limit (default 255);
   - the account's unlocked kinds.

   `IsStackable` and the stack limit read the policy for the player whose bag or vault it is. These read it:
   moves, pick-up, NPC buy, crafting counts, trade, the cash shop delivery. Server tests for each.
2. **Unlock with coins.**
   - The admin sets the price per kind.
   - The player buys an unlock from the vault window (or with `/stack buy <kind>`).
   - The coin receipt is recorded, and the unlock is saved before it counts (as the extra vault purchase).
3. **Vault pages.**
   - Defaults change to 3 free, VIP +1, buy more.
   - Page buttons 1..N in the web vault window send the switch (F5 group), and the window shows the page in use.
4. **Web client.**
   - Show the count on any stacked item, jewels too.
   - Split a stack (shift-drag, as the desktop's potion split).
   - Update the guide page and `PARITY.md` (an HNMO extra).
5. **Desktop client:** not changed unless the owner asks (parity rule, 2026-11-14). It shows the count only for the
   kinds it already stacks.

## Open questions for later

- **Jewels in the jewel bank:** they keep going there as before.
- **Unlock scope:** the account, all its characters.
