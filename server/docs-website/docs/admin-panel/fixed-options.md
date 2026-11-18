# Fixed Item Options (`/fixed-options`)

Lets hosts pin guaranteed options on matching items produced by monster drops or event chests/mini-game rewards.

- **Plug-in**: `FixedItemOptionsPlugIn` (**OFF by default** via `IDisabledByDefault`)
- **Rule fields**:
  - `ItemGroup` (`0..15` or `-1` wildcard) and `ItemNumber` (`-1` wildcard)
  - `SourceFilter`: `all`, `drop` (monster drops), or `box_event` (boxes & event rewards)
  - `MapNumber` (`-1` = any map)
  - `PinSkill`, `PinLuck`, `MinLevel`, `MinOptionLevel`, `MinExcellentCount`, `ForcedExcellentBits`
- **Hook point**: `DefaultDropGenerator` invokes `FixedItemOptionsPlugIn.ApplyToItems` on generated drops and `GenerateItemDrop` rewards without duplicating existing Luck or Excellent options.
