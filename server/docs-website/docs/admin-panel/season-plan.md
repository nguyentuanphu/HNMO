# Season plan

The **Season plan** lets the server open endgame progression features step by step (item level `+9` cap first, then `+10..+15` and Master levels, Harmony, Wing 2, Fenrir, 380 items, and Wing 3).

## Triggers & Conditions (`/season-plan`)

1. Enable **Enable Season Plan** (off by default).
2. Each stage unlocks automatically as soon as **any** configured condition is met:
   - **Unlock date (UTC / Sydney)**: scheduled release time.
   - **Boss kills**: a configured World Boss (e.g. `red-dragon`, `kundun`) killed at least N times.
   - **Players at level**: N unique characters reaching a target level.
   - **Manual admin control**: click **Unlock now** or **Lock** directly in `/season-plan`.
3. Locked features are refused cleanly before consuming Chaos Machine materials or jewels.
