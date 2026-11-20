# C2 F5 53 - HnmoArenaTravelState (by server)

## Is sent when

Sent after HnmoArenaTravelRequest (F5 52).

## Causes the following actions on the client side

The web client shows the Arena window; with a ticket it reloads into the Arena (or back home) with that ticket.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x53  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON: inArena (this server is the Arena), account, ticket (empty without travel), message (EN/VI by the player's language). |