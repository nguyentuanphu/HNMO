# Unit Audit: `client/src/source/GameLogic/Commands`

- **Unit**: `client/src/source/GameLogic/Commands`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `9` (`926` lines) including `ChatCommandCatalog.cpp`, `ChatCommandExecution.cpp`, `ChatCommandTemplate.cpp`, `ChatCommandValueLookup.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — holds client-side chat command argument validation and execution gating)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Command argument acceptance & bounds check | `ChatCommandCatalog.cpp:159-255` | `Accepts` validates command parameter types and numeric/string bounds against server-supplied `ChatCommandEntry` definitions before dispatch | Server `ChatCommands` (`ChatCommandPlugIn`) catalog schema | Same |
| Direct execution gate & `shortName=value` formatting | `ChatCommandCatalog.cpp:256-304` | `CanExecuteDirectly` filters commands that can execute without opening interactive prompts and serializes `shortName=value` arguments for server dispatch | Server `ChatCommandPlugIn` | Same |
