# Talismans, Figurines & Stat Consumables (`/talismans`)

The **Talismans & Stat Consumables** plug-in (`TalismanPlugIn`, OFF by default) gives administrators two customizable systems at `/talismans`:

## 1. Carried Bag Talismans / Figurines
- Define any item (`ItemGroup`, `ItemNumber`, optional minimum item level) that grants passive bonuses (`+Exp %`, `+Zen %`, `+Damage`, `+Defense`, `+Max HP`, `+Attack Speed`, `+Critical %`) or an existing `MagicEffectDefinition` buff while carried in the character's regular bag slots (`12–203`).
- Optionally enable stacking for duplicate copies up to a configured `MaxStacks`.
- Bonuses apply automatically when picked up or logged in and deactivate immediately when moved out of the bag.

## 2. Capped Stat-Point Consumables
- Define consumable items (`ItemGroup`, `ItemNumber`) that grant extra `Level-Up Points` or direct `Strength`, `Agility`, `Vitality`, `Energy`, or `Command` points on use.
- Per-character cumulative usage is persisted via `TalismanStore` (`HnmoPlayerStateRowStoreRegistry` / `HnmoStateStore`) so characters can never exceed the configured cap.
