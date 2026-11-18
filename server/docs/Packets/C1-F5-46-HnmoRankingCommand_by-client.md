# C1 F5 46 - HnmoRankingCommand (by client)

## Is sent when

Sent by the web client to request the in-game player, guild, online, and event rankings or submit a bulk stat-point distribution.

## Causes the following actions on the server side

The server processes the command and replies with HnmoRankingState (F5 47).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x46  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Action; 0 = RequestRankings, 1 = BulkAddStats. |
| 5 |  | String |  | Payload; Optional category filter or JSON stat allocation map (str, agi, vit, ene, cmd). |