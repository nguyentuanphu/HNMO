# C2 F5 49 - HnmoCustomRanksState (by server)

## Is sent when

Sent when entering the world, when active titles change, or after HnmoCustomRanksCommand.

## Causes the following actions on the client side

Updates the web client's custom title badges (nameplates and chat) and /title selector.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x49  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON payload listing rank tiers, the hero's unlocked and equipped rank, and online players' active titles. |