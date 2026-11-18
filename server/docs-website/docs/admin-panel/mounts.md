# Mount chain (`/mounts`)

Progressive mount crafting and account collection tracking Golden Boss shards and duplicate mounts.

## Highlights
- **Off by default**: enable in `/mounts` (`MountChainPlugIn`).
- **Golden Boss Shard drops**: killing Golden Bosses grants Mount Shards (configurable chance & amount).
- **Duplicate-mount upgrades & salvage**: players press `Shift+U` in-game (`🐴` on phones) to forge/upgrade `Uniria -> Dinorant -> Fenrir -> Golden Fenrir -> Royal Steed` using Mount Shards + unlocked duplicate bag mounts of the prior tier, or salvage extras into shards.
- **Modest PvE strength**: unlocked collection mounts apply small PvE bonuses (`DefenseFinal`, `BonusExperienceRate`, `MoneyAmountRate`) persisted in `HnmoStateStore` (`save/hnmo-state.json`).
- **Website & Account widget**: public progress renders at `home.html#mounts` (`/website/mounts`), plus standalone `client/web/src/ui/mountWidget.ts` for account pages.
