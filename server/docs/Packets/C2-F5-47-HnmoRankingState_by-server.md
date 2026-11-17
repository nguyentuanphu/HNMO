# C2 F5 47 - HnmoRankingState (by server)

## Is sent when

Sent after HnmoRankingCommand or /rank to deliver in-game player, online, guild, and event rankings and add-stats status.

## Causes the following actions on the client side

Updates the web client's Player Rankings window.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x47  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON payload containing ranked characters, online leaders, guilds, mini-game events, player rank summary, and bulk add-stats status. |