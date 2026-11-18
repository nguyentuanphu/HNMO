---
title: Item Definitions & Client Sync
sidebar_label: Item Definitions
---

# Item Definitions & Client Sync

Open `/item-definitions` in the Admin Panel to create new items or edit existing items in `GameConfiguration` alongside web/desktop client fields:

- **Names (EN / VI)**: saved to `ItemDefinition.Name` (`LocalizedString`) and exported for both web and desktop clients.
- **Size & Levels**: inventory cell width/height, drop/tier level, max enhancement level, durability, and zen worth.
- **Equip Requirements**: level, Strength, Agility, Vitality, Energy, and Leadership.
- **Glow Color & Tooltip Notes**: pick a `GlowColors.json` preset or a `#rrggbb` hex color, plus multi-line EN/VI tooltip notes shown in game tooltips.
- **Live Web Feed & Desktop Export**: saves persist to PostgreSQL and publish immediately through `GET /api/website/items` (`/website/items`). Use **Sync to Data Folder** (when running with loose `Data/Items/` files) or **Download Client Export (.zip)** (`/api/admin/item-definitions/export`) to export `Data/Items/GroupNN_*.json`, `Data/Items/Models/GroupNN_*.json`, and `Data/Effects/GlowColors.json` from the same source.
