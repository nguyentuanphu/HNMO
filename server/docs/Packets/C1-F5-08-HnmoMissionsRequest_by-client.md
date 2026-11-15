# C1 F5 08 - HnmoMissionsRequest (by client)

## Is sent when

The player opens HNMO's missions window.

## Causes the following actions on the server side

The server sends the HnmoMissionsState message, and from then on sends it again when something changes, e.g. a mission is finished.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   4   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x08  | Packet header - sub packet type identifier |