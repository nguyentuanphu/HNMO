# C2 F5 09 - HnmoMissionsState (by server)

## Is sent when

After the client requested HNMO's missions (HnmoMissionsRequest) or did an action there, and when something changed for a client which requested them before (a mission was finished, earned coins were given).

## Causes the following actions on the client side

The missions window shows the state; a notice in the chat tells about the event in it (e.g. a finished mission or the result of a claim).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x09  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; The state as JSON, in plain ASCII (other characters are escaped): the daily and weekly missions with their progress and rewards, the streak, the login calendar, the online time coins, the earned coins of the day and the invite code, and the event which caused it, if any. |