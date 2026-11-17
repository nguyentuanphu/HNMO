# C1 F5 48 - HnmoCustomRanksCommand (by client)

## Is sent when

Sent by the web client to request custom rank definitions and active player titles, or to equip/reset a player's displayed title.

## Causes the following actions on the server side

The server processes the command and replies with HnmoCustomRanksState (F5 49).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x48  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Action; 0 = RequestState, 1 = SelectRank (empty RankId restores automatic highest rank). |
| 5 |  | String |  | RankId; The rank tier identifier to equip, or empty for automatic highest tier. |