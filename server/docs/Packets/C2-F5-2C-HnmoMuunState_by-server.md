# C2 F5 2C - HnmoMuunState (by server)

## Is sent when

Sent after HnmoMuunCommand, and when the active Muun's bonus changes (map, level, hour, weekday) for a client which asked for the state once.

## Causes the following actions on the client side

Updates the web client's Season 9 Muun window.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x2C  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON payload: whether Muuns are on, the active Muun and its bonus or unmet condition, the Muuns in the bag, the energy, the costs and an optional event text. |