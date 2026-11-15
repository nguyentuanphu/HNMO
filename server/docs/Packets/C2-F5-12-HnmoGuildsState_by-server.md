# C2 F5 12 - HnmoGuildsState (by server)

## Is sent when

After the client requested HNMO's guild levels (HnmoGuildsRequest) or did an action there, and when something changed for a client which requested them before (the guild reached a level or won the castle siege, a ladder reward was given).

## Causes the following actions on the client side

The guild window shows the guild's level, experience, ladder rank and buff; a notice in the chat tells about the event in it (e.g. a level-up or a reward).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x12  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; The state as JSON, in plain ASCII (other characters are escaped): the guild, its level and experience, this week's ladder points and rank, the member's part, the buff, the ladder's top guilds and rewards, the waiting rewards, and the event which caused it, if any. |