---
sidebar_position: 42
---

# Gift Box / Mailbox

The **Gift Box** admin page (`/gift-box`) lets hosts compose localized EN/VI mail deliveries containing optional items, Zen, or account coins for one account, a pasted list of accounts, active accounts from the last N days, or every account.

## Sending a Gift

1. Open **Gift Box** (`/gift-box`) from the Admin Panel.
2. Enter English and Vietnamese titles and messages (with optional Sydney expiry date/time).
3. Choose the target audience (`Single account`, `Account list`, `Active in last N days`, or `Everyone`) and enter the required target/days.
4. Optionally select an item from the catalog item picker (level, skill, luck, option, excellent flags), Zen, or coin currency + amount.
5. Click **Preview delivery** to review the exact matched recipient count and summary, then type a required audit reason and click **Confirm & Send**.

If confirmation encounters an uncertain persistence outcome, retry using the same confirmation flow rather than creating a new draft so idempotency prevents duplicate grants.

## Player Collection

- **Account page (`#rewards`):** Offline collection acquires the login server's exclusive account lease before crediting the selected character's inventory, Zen, or coins.
- **In-game window (`Alt+M`):** Collects directly into the currently entered character under the player's exclusive persistence lock (`F5 27` / `F5 28`).
- Full-bag deliveries keep uncollected items owed without losing anything, and successful collections record an entry in the Admin Panel activity log.
