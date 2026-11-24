# Unit Audit: `client/src/source/UI/Chat`

- **Unit**: `client/src/source/UI/Chat`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `9` (`1,100` lines) including `Whisper.cpp`, `Chat.cpp`, `SystemLogBox.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — `Whisper.cpp` enforces the client Whisper minimum level check and reply whitelist)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Whisper minimum level gate & reply-list exception | `Whisper.cpp:12,20-67` | Requires `CharacterAttribute->Level >= requiredLevel` (level `6`, per `YouCanUseTheWhisperCommandAtCharacterLevel6` error at `:35`); characters below `requiredLevel` may still reply to up to `WHISPER_ID_SLOTS = 10` senders who whispered them (`Register` caches incoming senders) | Server `ChatMessage` / Whisper level 6 rule | Same |
| Chat rendering & guild alliance tags | `Chat.cpp:403,409` & remaining UI files | Chat bubble formatting (`<UnionName>`), log scrolling, input box handling | UI presentation | No game rules |
