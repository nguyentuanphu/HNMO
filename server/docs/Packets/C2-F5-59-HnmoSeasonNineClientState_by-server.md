# C2 F5 59 - HnmoSeasonNineClientState (by server)

## Is sent when

Sent upon HnmoSeasonNineClientRequest, in a Season 9 realm.

## Causes the following actions on the client side

The web client applies the Season 9 client settings of the realm (e.g. the Season 9 minimap markers).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x59  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON payload: {"v":1,"minimap":bool}. |