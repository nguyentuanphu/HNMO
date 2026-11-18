---
sidebar_position: 43
---

# Bans & Mutes Manager

The **Bans & Mutes** page (`/bans`) lets operators and administrators issue and lift timed or permanent moderation actions:

- **Account Ban / Character Ban**: Blocks login or character world entry while showing the player-facing bilingual (EN/VI) reason and exact **Australia/Sydney** expiration timestamp.
- **Account Mute / Character Mute**: Blocks all in-game chat channels while displaying the mute reason and Sydney expiration time directly to the player.
- **Network / Device Bans**: Computes a keyed HMAC-SHA256 digest using a persistent 256-bit server salt (`moderation.salt`). Raw IP addresses and raw device tokens are never persisted or exposed in audit logs.
- **Search & Audit**: Filter records by target, reason, issuer, or active status, and lift restrictions with a mandatory audit reason recorded in both the Admin Audit Log and Player Activity Log.
