# Top-up milestones (Thưởng mốc nạp)

Reward players once per cumulative VND top-up step in the game (`U` key) and on the website.

## Enabling and configuring

1. Open **Rules and Events → Top-up milestones** (`/topup-milestones`) in the Admin Panel.
2. Turn **Active** on (the plug-in starts **OFF** by default).
3. Configure the milestone **Steps** (unique ID, cumulative VND threshold, English & Vietnamese titles, coin type, coins, zen, and optional items as `group:number:level:quantity`).
4. Click **Save**.

## How transfers are counted

- **Automatic**: Casso and SePay webhooks in `BankTopUpService` record each transfer's VND amount idempotently by bank reference into `save/hnmo-state.json` (`topUpMilestones`).
- **Manual**: Use the **Confirm bank transfer / record VND** section on `/topup-milestones` with the player's login name, VND amount, and a unique transfer reference.
- On the public `/topup.html` page, VietQR codes are generated locally in the browser with no external service calls.
