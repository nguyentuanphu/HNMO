# Unit: client/src/source/Data/GameData/ItemData (files 1)

Checked against: Server `ItemDefinition` & Web `items.ts`.

Scope: `client/src/source/Data/GameData/ItemData (files 1)` (`21` files, `5417` lines).

## Findings

| File & Line | Rule / Constant | Parity Reference | Result |
|---|---|---|---|
| `ItemDefinition.h:124-211` | Validates per-level item fields within `[0 .. ItemLevelCount - 1]` | Client/Server item level range | Same |
