# 3D World Editor

The **3D World Editor** (`/world-editor`) renders each map inside the admin panel using the HNMO web game's own Three.js terrain and world pipeline.

## Capabilities

- **Player & Orbit Camera Modes**: Inspect any coordinate from the player's isometric angle or orbit freely in 3D.
- **Terrain Zone Painting**: Paint `Safezone`, `Blocked`, `NoGround`, `Water`, or clear tiles back to walkable directly on the 3D terrain mesh with adjustable brush radius and Undo/Redo history.
- **Monster Spawns & Gates**: Click on the 3D ground to place `MonsterSpawnArea`, `EnterGate`, and `ExitGate` zones and tune coordinates, quantities, triggers, and destinations.
- **3D Objects & Scene Lights**: Place, move, rotate, scale, or delete static world objects (`EncTerrainN.obj`), preview ambient scene lighting, and paint directly onto `TerrainLight.jpg`.
- **Minimap & Map Pack Rebuild**: Regenerate the top-down `mini_map.tga` / `mini_map.ozt` in one click and rebuild `worldN.hpk` packs cleanly alongside synchronized DB + Data saves.
