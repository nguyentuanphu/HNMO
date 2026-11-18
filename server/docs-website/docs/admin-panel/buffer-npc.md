# Buffer NPC

The **Buffer NPC** plug-in (`BufferNpcPlugIn`, **off by default**) lets hosts grant a configurable bundle of helpful buffs to new characters up to a chosen level and optional reset limit when they talk to Elf Soldier (`257`) or another configured Buffer NPC.

## Configuration (`/buffer-npc`)

1. Toggle **Enable Buffer NPC** on (off by default — while off, Elf Soldier grants only the classic single buff).
2. Set **Max level (inclusive)** (default `220`) and **Max resets** (`-1` ignores resets).
3. Choose **Duration (seconds)** (default `1800`s = 30 min; `0` uses each effect's built-in duration) and optional **Zen cost**.
4. Pick the buffs from the preset list (`Elf Soldier Buff`, `Greater Damage`, `Greater Defense`, `Soul Barrier`, `Critical Damage Increase`, `Ability Recover Speed Increase`, `Greater Fortitude`) and/or comma-separated extra `MagicEffectDefinition` IDs.
5. Configure **Buffer NPC numbers** (default `257` = Elf Soldier in Lorencia, Noria, Devias, Elbeland) and click **Save changes**.

Players receive localized English or Vietnamese blue chat notices when granted buffs or if they exceed the level/reset limit or lack Zen.
