# C2 F5 51 - HnmoItemStackState (by server)

## Is sent when

Sent after HnmoItemStackCommand (F5 50).

## Causes the following actions on the client side

The web client knows which items stack and how many (its counts and the split), and shows the stack kinds with their unlocks.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x51  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON: enabled, coinType, balance, kinds (key, name, free, unlocked, limit, price, items), result message (EN/VI by the player's language). |