---
id: worlds
title: Worlds & Multi-Realm Management
sidebar_label: Worlds
---

# Worlds & Multi-Realm Management (`/worlds`)

Manage multiple isolated game worlds backed by separate databases from one Admin Panel (ROADMAP 15.7).

## Capabilities

- **Start / Stop per world**: Control all assigned sub-servers belonging to a world together.
- **Per-world rates & events**: Configure EXP, Master EXP, Drop, and Zen multipliers plus EN/VI event presets independently per world.
- **Create a world from a template**:
  - Clone settings (`GameConfiguration` rates and plug-in configurations) from an existing template world.
  - Choose **Fresh** (empty character roster) or **CopiedCharacters** (clone accounts and characters from the template world).
  - Set an optional **Opening date & time in Sydney time (`Australia/Sydney`)** so the website and server picker lock/display the opening countdown until launch.
- **One-off character transfer**: Atomically transfer an eligible character once between separate world databases, blocking duplicate transfers of the same character.
- **Grouped website & server list**: `/api/website`, `/api/website/worlds`, the website realm selector (`home.html`), and the web client server picker group sub-servers by world.
