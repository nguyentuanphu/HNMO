# Unit: client/src/source/GameLogic/Events

Checked against: Web `events.ts` & Server BC/CC/DS event countdown.

Scope: `client/src/source/GameLogic/Events` (`26` files, `5654` lines).

## Findings

| File & Line | Rule / Constant | Parity Reference | Result |
|---|---|---|---|
| `CSEventMatch.cpp:27,168-175` | `kMatchCountdownDuration = 30s` match countdown timer | Server 30s BC/CC/DS close countdown & Web `events.ts` | Same |
