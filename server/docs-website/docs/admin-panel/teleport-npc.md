# Teleport NPC (`/teleport-npc`)

Lets hosts designate NPCs (e.g., `247`, `540`) that open an interactive Teleport NPC menu letting players warp to chosen maps for **Zen**, with optional **Min/Max Level**, **Min Resets**, **VIP**, and **PK** gates.

- **Plug-in**: `TeleportNpcPlugIn` (**OFF by default** via `IDisabledByDefault`)
- **Packets**: `HnmoTeleportNpcCommand` (`F5 4A`), `HnmoTeleportNpcState` (`F5 4B`)
- **Server validation**: `TeleportNpcEngine.EvaluateWarp` enforces all gates and deducts Zen atomically via `Player.TryRemoveMoney` before `Player.WarpToAsync`.
- **Web Client**: `TeleportNpcWindow` (`client/web/src/ui/teleportNpcWindow.ts`) opens automatically when talking to a matching NPC.
