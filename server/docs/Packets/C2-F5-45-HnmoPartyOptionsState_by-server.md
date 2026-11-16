# C2 F5 45 - HnmoPartyOptionsState (by server)

## Is sent when

Sent after HnmoPartyOptionsCommand or when opening the Party window options drawer.

## Causes the following actions on the client side

Updates the web client's Party password and Auto-party spot finder drawer.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x45  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON payload listing active party advertisement state and open hunting spot parties. |