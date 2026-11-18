---
sidebar_position: 20
title: Host settings
---

# Host settings

The **Host settings** page (`System -> Host settings` in the admin panel) stores all host-level settings in a single file: `save/hnmo-settings.json`.

On the first start after upgrading, HNMO automatically migrates existing values from `web/appsettings.json` and `server/ConnectionSettings.xml` into `save/hnmo-settings.json`.

## Configurable settings

- **Ports & public address**: Web port (`8080`), Admin port (`8095`), Connect server port (`44406`), Database port (`55432`), Public address, and Open browser URL.
- **Web bridge & launcher**: `WebUrls`, `WebRoot`, `DataPath`, `VendorPath`, `RelayAllowedHosts`, `RelayAllowLoopback`, `RelayConnectTimeoutSeconds`, `LauncherEnabled`, `PostgresPath`, `SavePath`, `ServerPath`, and `ServerProgram`.
- **Database connection**: `DatabaseHost`, `DatabaseName`, `DatabaseUser`, and password updates (the current password is never displayed).
- **Player license**: License file path, active limit/machine ID status, and `.hnmo` license upload.

## Restarting HNMO

Changes to ports, URLs, folder paths, or database connection require a restart. Use the **Restart HNMO** button on the page to disconnect players cleanly, save progress, and restart both the game server and web bridge.
