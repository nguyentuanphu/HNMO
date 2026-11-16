# C1 F5 42 - HnmoMountChainCommand (by client)

## Is sent when

Sent by the web client to open the Mount Chain collection (Shift+U), craft/upgrade a mount step, or claim/summon a collected mount.

## Causes the following actions on the server side

The server processes the mount chain action and sends HnmoMountChainState (F5 43).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x42  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Action; 0 = RequestState, 1 = CraftOrUpgrade, 2 = SummonUnlocked. |
| 5 |  | String |  | Id; Target mount tier or recipe identifier. |