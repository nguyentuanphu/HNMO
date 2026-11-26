# Unit Audit: `client/src/source/UI/HUD (files 1)`

- **Unit**: `client/src/source/UI/HUD (files 1)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `21` (`5,772` lines) including `ChatInputBox.cpp`, `ChatInputBox.h`, `CommandWindow.cpp`, `GensRanking.cpp`, `HotKey.cpp`, `ItemHotKey.cpp`, `BuffStrip.cpp`, `ChatLogWindow.cpp`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Chat spam cooldown & prefix routing | `ChatInputBox.cpp:327-355`, `ChatInputBox.h:67` | Enforces `ChatCooldownMs` cooldown between sent messages; Chaos Castle hides ChatInputBox unless `INTERFACE_CHAOSCASTLE_TIME` is visible (`314`); prefixes `~` party, `@` guild, `$` gens | Web `chat.ts:48` (`COOLDOWN_MS = 1000`) & Server `Chat` | Same |
| Safezone mount state check for chat/commands | `ChatInputBox.cpp:398` | Prevents mount actions while `Hero->SafeZone` or not riding valid mount | Server mount rules | Same |
| Hotkeys, Command Window & Item Q/W/E/R registration | `CommandWindow.cpp:42-44`, `HotKey.cpp:240`, `ItemHotKey.cpp` | Proximity player commands (Trade, Buy, Party, Whisper, AddFriend, Follow, Duel, etc.); Master Level tree unlock hotkey (`M` / `A`); Q/W/E/R potion registration rules | Web `commandWindow.ts`, `itemUse.ts` | Same |
| Buff strip & Gens window bindings | `BuffStrip.cpp`, `GensRanking.cpp` | Family-deduped buff icons and Gens ranking/mark rendering | Web `buffStrip.ts` & `gensWindow.ts` | Same |
