# C1 F5 4A - HnmoTeleportNpcCommand (by client)

## Is sent when

Sent by the web client to request the Teleport NPC destination list or warp to a selected destination.

## Causes the following actions on the server side

The server validates requirements and Zen fee, warps the character if eligible, and replies with HnmoTeleportNpcState (F5 4B).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x4A  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Action; 0 = RequestMenu, 1 = WarpToDestination. |
| 5 |  | String |  | DestinationId; Target destination identifier from HnmoTeleportNpcState. |