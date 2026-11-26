# Unit Audit: `client/src/source/UI/Social (files 1)`

- **Unit**: `client/src/source/UI/Social (files 1)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `38` (`5,928` lines) including `LetterWriteWindow.cpp`, `FriendWindow.cpp`, `ChatRoomWindow.cpp`, `PhotoViewer.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — `LetterWriteWindow.cpp` normalises and enforces the client max mail letter length and required fields)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| In-game letter max body length & field checks | `LetterWriteWindow.cpp:15-34,114-140` | Normalises CRLF pairs and truncates letter text at `MAX_LETTERTEXT_LENGTH` (`1000` bytes/chars); rejects empty recipient, title, or body locally | Web `friendWindows.ts:470` (`MAX_LETTER_TEXT = 1000`) & `FriendServer` | Same |
| Friend list / chat room / photo windows | Remaining 36 files | Friend messenger shell, whisper chat room layout, 3D portrait preview (`PhotoViewer`) | UI presentation | No game rules |
