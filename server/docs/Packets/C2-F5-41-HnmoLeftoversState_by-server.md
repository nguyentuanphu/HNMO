# C2 F5 41 - HnmoLeftoversState (by server)

## Is sent when

Sent when entering the world, talking to the exchange NPC, or after HnmoLeftoversCommand.

## Causes the following actions on the client side

Updates the web client's Combining Leftovers & Exchange NPC window (F10).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x41  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON payload containing recipes, exchange offers, bag counts, and season box cap. |