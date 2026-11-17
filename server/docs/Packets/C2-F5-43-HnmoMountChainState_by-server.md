# C2 F5 43 - HnmoMountChainState (by server)

## Is sent when

Sent when entering the world, defeating a golden boss, or after HnmoMountChainCommand.

## Causes the following actions on the client side

Updates the web client's Mount Chain window (Shift+U).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x43  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON payload describing mount chain tiers, shards, duplicate bag mounts, and unlocked collection. |