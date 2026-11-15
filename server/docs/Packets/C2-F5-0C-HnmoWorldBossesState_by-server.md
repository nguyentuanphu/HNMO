# C2 F5 0C - HnmoWorldBossesState (by server)

## Is sent when

After the client requested HNMO's world bosses (HnmoWorldBossesRequest) or did an action there, and when something changed for a client which requested them before (a boss was announced, appeared, was hit, killed or left; rewards were given).

## Causes the following actions on the client side

The world boss window and the line on the screen show the state; a notice in the chat tells about the event in it (e.g. an announcement or a reward).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x0C  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; The state as JSON, in plain ASCII (other characters are escaped): the bosses with their next spawn, the alive ones with their time left and damage table, the player's damage and rank, the waiting rewards, the boss-hunt points, and the event which caused it, if any. |