# C1 F5 56 - HnmoUltimateAutoPlayRequest (by client)

## Is sent when

Sent by the web client on login or when opening Ultimate Autoplay to fetch the current policy.

## Causes the following actions on the server side

The server answers with HnmoUltimateAutoPlayPolicy (F5 57).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   4   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x56  | Packet header - sub packet type identifier |