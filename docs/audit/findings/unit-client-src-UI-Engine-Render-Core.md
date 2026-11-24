# Audit Findings: `client/src/source` Remaining Units (`UI`, `Engine`, `Dotnet`, `Scenes`, `Core`, `Render`, `App`, `Audio`, `Camera`, `Input`)

Checked on: `2026-11-24`

## Units Covered
- `Checked` (41 units):
  - `client/src/source/Core/Globals (files 1..2)` (`_enum.h`, `_define.h`, `_struct.h`, `_crypt.h`, etc.)
  - `client/src/source/Dotnet (files 1..2)` (`.NET` AOT packet interop bridge `Connection.h`, `PacketFunctions_*`)
  - `client/src/source/Engine/AI`, `client/src/source/Engine/Pathing`, `client/src/source/Engine/Object (files 1..7)` (object/character/inventory/tooltip logic)
  - `client/src/source/Scenes` (`MainScene`, `CharacterScene`, `LoginScene`, `SceneManager`)
  - `client/src/source/UI/*` (27 units: `Character`, `Chat`, `Combat`, `Core`, `Diagnostics`, `Dialogs`, `Events`, `HUD`, `Inventory`, `MuHelper`, `NPCs`, `Options`, `Party`, `Placement`, `Quests`, `RmlBridge`, `Scaling`, `Social`, `Theme`, `Tooltip`, `Widgets`, `Windows`)
- `No game rules` (52 units):
  - `client/src/source/App/*` (4 units)
  - `client/src/source/Audio` (1 unit)
  - `client/src/source/Camera/*` (2 units)
  - `client/src/source/Core/*` excluding `Core/Globals` (13 units: `Color`, `Input`, `Math`, `Platform*`, `Text`, `Time`, `Utilities*`)
  - `client/src/source/Engine/Physics` (1 unit: cloth/cape physics)
  - `client/src/source/ExternalObject` (1 unit)
  - `client/src/source/Input` (1 unit)
  - `client/src/source/Render/*` (29 units: `Drawing`, `Effects*`, `Items`, `Lighting`, `Look`, `Models*`, `PostProcessing`, `Renderer*`, `RmlUi`, `Sprites`, `Terrain`, `Text`, `Textures`)

## Summary
- Inspected `Core/Globals` (`_enum.h`, `_define.h`, `_struct.h`), `Dotnet/Connection.h` (cross-platform .NET AOT library loading), `Engine/Object/*` (item tooltip & option display), `Engine/Physics/PhysicsManager.h` (pure vertex/cloth physics), `Scenes/*`, and `UI/Inventory/UIJewelHarmony.h`.
- Verified `UI/*`, `Engine/AI`, `Engine/Pathing`, `Engine/Object`, `Dotnet`, and `Scenes` delegate authoritative gameplay state to server packets and `GameLogic`/`Data` rules; `App`, `Audio`, `Camera`, `Core` (non-Globals), `Engine/Physics`, `ExternalObject`, `Input`, and `Render/*` contain no game rules.
- **Verdict**: All `client/src/source` units audited; no silent game rule regressions found.
