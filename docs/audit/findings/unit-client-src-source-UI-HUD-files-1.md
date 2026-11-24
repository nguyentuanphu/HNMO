# Unit: client/src/source/UI/HUD (files 1)

Checked against: classic client chat spam cooldown.

Scope: `client/src/source/UI/HUD (files 1)` (`21` files, `5772` lines).

## Findings

| File & Line | Rule / Constant | Role & Value | Result |
|---|---|---|---|
| `ChatInputBox.h:67`, `ChatInputBox.cpp:329` | `ChatCooldownMs = 1000` (`1` second) client send throttle | Client chat anti-spam cooldown | Same |
