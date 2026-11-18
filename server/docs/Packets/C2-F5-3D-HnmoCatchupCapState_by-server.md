# C2 F5 3D - HnmoCatchupCapState (by server)

## Is sent when

Sent on entering the world, when today's cap increases, or upon HnmoCatchupCapRequest.

## Causes the following actions on the client side

Updates today's Catch-up Level/Reset Cap notice in the web client.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x3D  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON payload containing today's level cap, daily reset cap, first achiever, and next Sydney day reset countdown. |