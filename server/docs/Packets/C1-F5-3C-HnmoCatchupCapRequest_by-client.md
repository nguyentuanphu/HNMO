# C1 F5 3C - HnmoCatchupCapRequest (by client)

## Is sent when

Sent by the web client to query today's Catch-up Level and Reset Cap state.

## Causes the following actions on the server side

The server responds with HnmoCatchupCapState (F5 3D).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   4   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x3C  | Packet header - sub packet type identifier |