# C1 F5 11 - HnmoGuildsRequest (by client)

## Is sent when

The web game entered the world, or the player opens the guild window.

## Causes the following actions on the server side

The server sends the HnmoGuildsState message, and from then on sends it again when something changes, e.g. the guild reached a level or a ladder reward was given.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   4   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x11  | Packet header - sub packet type identifier |