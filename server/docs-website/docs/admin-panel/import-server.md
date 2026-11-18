---
title: Import from Another Server
sidebar_label: Import Server
---

# Import from Another Server

Open `/import-server` in the Admin Panel to migrate data and settings from a local legacy server configuration folder and optional local MSSQL database directly into HNMO.

## Supported Categories
- **Server Rates**: Normal EXP, Master EXP, Drop %, and Zen multiplier.
- **Item Drop Lists**: `DropManager` XML drop groups matching existing HNMO items & monsters (Season 9 elemental/companion drops are flagged and skipped safely).
- **NPC Shops**: `ShopList` XML + referenced `.xml` / `.txt` merchant inventories.
- **Event Schedules**: Timetable entries and map bonus schedules (Season 9-only events are reported clearly and skipped).
- **Chaos Machine Rates**: `+10` through `+15` mix success percentages and Luck bonus into `ChaosRatesPlugIn`.
- **Monster & NPC Spawns**: Point, area, and event spawn rules from `MonsterSetBase` text files.
- **Optional MSSQL Database Migration**: Reads Accounts, Characters (stats, zen, map coordinates, decoded binary inventory), Account Vaults (zen & items), and Guilds/Members. Passwords from legacy databases are **never** stored in plaintext—accounts receive a fresh BCrypt-hashed random reset token or a BCrypt hash of your temporary migration passphrase.

## Safe 3-Step Workflow
1. **Run Dry-Run Preview**: Detects Season 6 vs Season 9 automatically and lists all mappable vs. skipped items before modifying anything.
2. **Create Backup & Apply Import**: Saves a complete atomic backup under `save/import-backups/<backupId>.json` before writing changes.
3. **Undo Import**: Rolls back settings, merchant shops, plugin configs, and removes imported records from that batch in one click.
