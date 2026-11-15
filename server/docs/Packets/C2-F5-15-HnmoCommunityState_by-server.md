# C2 F5 15 - HnmoCommunityState (by server)

## Is sent when

After the client requested HNMO's community state (HnmoCommunityRequest) or did an action there, and when something changed for a client which requested it before (community coins, an event reward, an approved Zalo claim).

## Causes the following actions on the client side

The community window shows the one-time rewards, the community events and the waiting rewards; a notice in the chat tells about the event in it.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x15  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; The state as JSON, in plain ASCII (other characters are escaped): the Discord and Zalo rewards and the player's state of them, the community events, the waiting rewards, and the event which caused it, if any. |