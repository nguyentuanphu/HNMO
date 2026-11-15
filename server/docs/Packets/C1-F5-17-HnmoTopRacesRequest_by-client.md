# C1 F5 17 - HnmoTopRacesRequest (by client)

## Is sent when

The web game entered the world, or the player opens the top races window.

## Causes the following actions on the server side

The server sends the HnmoTopRacesState message, and from then on sends it again when something changes, e.g. a race started or ended or a prize was given.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   4   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x17  | Packet header - sub packet type identifier |