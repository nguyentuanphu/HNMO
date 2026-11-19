# C2 F5 2A - HnmoNewsNotice (by server)

## Is sent when

Sent when entering the world with a published post or when an admin publishes a new news post.

## Causes the following actions on the client side

Shows a bilingual news notice in the in-game chat box.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x2A  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; JSON payload containing the latest published EN/VI news post and fresh flag. |