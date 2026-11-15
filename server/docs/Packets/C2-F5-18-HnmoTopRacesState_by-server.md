# C2 F5 18 - HnmoTopRacesState (by server)

## Is sent when

After the client requested HNMO's top races (HnmoTopRacesRequest) or did an action there, and when something changed for a client which requested them before (a race started or ended, a prize was given).

## Causes the following actions on the client side

The top races window shows the running races with the player's rank and the top 10, the prizes, the time left, the past winners and the waiting prizes; a notice in the chat tells about the event in it.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x18  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; The state as JSON, in plain ASCII (other characters are escaped): the running and coming races with their prizes, the player's rank and value, the tables, the past winners, the waiting prizes, and the event which caused it, if any. |