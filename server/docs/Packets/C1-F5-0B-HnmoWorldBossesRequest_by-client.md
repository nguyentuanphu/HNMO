# C1 F5 0B - HnmoWorldBossesRequest (by client)

## Is sent when

The web game entered the world, or the player opens HNMO's world boss window.

## Causes the following actions on the server side

The server sends the HnmoWorldBossesState message, and from then on sends it again when something changes, e.g. a boss is announced, appears, is hit or killed.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   4   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x0B  | Packet header - sub packet type identifier |