# C1 F5 3E - HnmoSeasonPlanRequest (by client)

## Is sent when

Sent by the web client to open or refresh the Season Plan timeline (F8).

## Causes the following actions on the server side

The server replies with HnmoSeasonPlanState (F5 3F).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   4   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x3E  | Packet header - sub packet type identifier |