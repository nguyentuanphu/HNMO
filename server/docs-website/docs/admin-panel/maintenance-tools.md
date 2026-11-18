---
title: Maintenance Tools
---

# Maintenance Tools

The **Maintenance Tools** page (`/maintenance-tools`) gives administrators dry-run previews and audited cleanup actions:

1. **Inactive & empty accounts**: Finds player accounts registered at least *N* days ago (optionally filtering to accounts with zero characters and empty vaults) and lets you purge them safely after previewing.
2. **Duplicate item serial / GUID check**: Scans all player vaults and character inventories to detect identical item GUIDs occurring in more than one slot, and lets you remove extra copies while keeping the first occurrence.
3. **Character experience fixer**: Identifies characters whose stored `Experience` falls below their current level's minimum threshold or exceeds the cap without having leveled up, and clamps values back into valid bounds.
4. **Backup / restore link**: Direct link to `/control` for creating or restoring full PostgreSQL backups prior to cleanup.

Every **Apply** action executes through `AdminOperationExecutor` and writes an entry to the admin audit log.
