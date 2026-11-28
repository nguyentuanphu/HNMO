# C1 F5 58 - HnmoSeasonNineClientRequest (by client)

## Is sent when

Sent by the web client after entering the world in a Season 9 realm.

## Causes the following actions on the server side

The server responds with HnmoSeasonNineClientState (F5 59).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   4   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x58  | Packet header - sub packet type identifier |