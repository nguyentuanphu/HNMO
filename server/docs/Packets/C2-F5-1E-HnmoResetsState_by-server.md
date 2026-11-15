# C2 F5 1E - HnmoResetsState (by server)

## Is sent when

After the client requested HNMO's resets (HnmoResetsRequest) or did an action there, and when something changed for a client which requested them before (a reset with the NPC or a chat command, a relife, delegated resets done while away).

## Causes the following actions on the client side

The web game updates the character's resets and relifes and shows a notice in the chat about the event in it.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x1E  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; The state as JSON, in plain ASCII (other characters are escaped): resets, relifes, today's resets and limits, the delegated reset, the next reset and relife with their needs and costs, and the event which caused it, if any. |