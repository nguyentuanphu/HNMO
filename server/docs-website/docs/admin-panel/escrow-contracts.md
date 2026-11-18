---
title: Escrow Contracts
---

Configure and moderate in-game **Escrow Contracts** (`/escrow-contracts`):

- **Goods supported**: Seller inventory items (with full options & Activity Log origin), Zen, and Jewel Bank jewels.
- **Payment currencies**: Zen, WCoinC, WCoinP, GoblinPoints, or Jewel Bank jewels, subject to admin allowlist.
- **Safety**: Second password verification (`AccountSafety`), extra high-value confirmation thresholds, minimum account age (days), post-escrow item trade cooldown (hours), per-account reputation, and admin freeze / force-cancel controls.
- **In-game & Website**: Players open the Contracts window via `F9`, `/contract`, or Command Window (`D` -> **Contract** -> right-click player), and can inspect/cancel contracts from the website `/portal/escrow/*` account section.
- **Direct P2P VND Mode (OFF by default)**: Requires ticking the Vietnamese regulatory compliance acknowledgement first. Seller bank details remain hidden until a buyer accepts and reserves the contract (30-min payment window extendable once), rendering the seller's NAPAS VietQR (`HNMO-XXXX`) directly for buyer payment, with image receipt upload, manual seller confirmation, per-seller HMAC-SHA256 webhook confirmation (`POST /api/website/escrow/direct/webhook/{contractId}`), and admin dispute evidence resolution (with bond forfeiture & optional account ban).
- **Economy Dashboard**: Completed contract fees appear automatically on `/economy-dashboard`.
