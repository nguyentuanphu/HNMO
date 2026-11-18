# C2 F5 4B - HnmoTeleportNpcState (by server)

## Is sent when

Sent when talking to a Teleport NPC or after HnmoTeleportNpcCommand.

## Causes the following actions on the client side

Opens or updates the Teleport NPC map-warp menu in the web client.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x4B  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON payload containing Teleport NPC destinations, requirements, Zen fees, and warp result status. |