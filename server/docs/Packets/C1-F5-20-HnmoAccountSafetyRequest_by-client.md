# C1 F5 20 - HnmoAccountSafetyRequest (by client)

## Is sent when

The web game entered the world.

## Causes the following actions on the server side

The server sends the HnmoAccountSafetyState message, and from then on sends it again when something changes, e.g. an action needs the second password, an item was locked or unlocked, or a locked item moved.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   4   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x20  | Packet header - sub packet type identifier |