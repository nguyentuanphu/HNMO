# Custom Ranks & Titles (`/custom-ranks`)

Admin-defined titles awarded by character **Level**, **Master Level**, **Resets**, **VIP Level**, claimed **Achievements/Missions**, or specially granted achievement keys.

- **Plug-in**: `CustomRanksPlugIn` (**OFF by default** via `IDisabledByDefault`)
- **Packets**: `HnmoCustomRanksCommand` (`F5 48`), `HnmoCustomRanksState` (`F5 49`)
- **In-game display**: Active title badges appear beside the player's overhead nameplate (`ShowByNameplate`) and/or prefix their chat messages (`ShowInChat`) using EN/VI localization.
- **Player command**: `/title` lists unlocked titles; `/title <id|auto>` switches the displayed badge or returns to automatic highest-priority selection.
- **Persistence**: Character preferences and granted achievement keys persist via `HnmoStateStore` (`custom-ranks-state.json`) and PostgreSQL `customRanks.character` row sync.
