# Combining leftovers & surplus exchange (`/leftovers`)

Lets players turn spare Kundun boxes and surplus event/boss items into useful upgrades or Goblin Points without cluttering bags.

## Features
- **Off by default**: enable in `/leftovers` (`CombiningLeftoversPlugIn`).
- **Box upgrade recipes**: default `Box +1 -> +2 -> +3 -> +4 -> +5` recipes consume 2 lower boxes, Jewel of Life, Jewel of Creation, and Zen. Recipes above the current Season Plan `MaxBoxLevel` stay locked until that season stage opens.
- **Surplus exchange offers**: trade surplus items (such as Rena or Sign of Lord) in-game (`F10` or speaking to Exchange NPC `546`) for Goblin Points, Zen, or items.
- **Safety & reliability**: Account Safety locked items are excluded automatically; rewards that do not fit in the bag are owed in `HnmoStateStore` (`save/hnmo-state.json`) and delivered on next login or space freed.
- **Website visibility**: active recipes and exchange offers appear publicly on `home.html#leftovers` (`/website/leftovers`).
