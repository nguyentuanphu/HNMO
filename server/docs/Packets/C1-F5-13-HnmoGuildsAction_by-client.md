# C1 F5 13 - HnmoGuildsAction (by client)

## Is sent when

The player clicks a button of HNMO's guild levels panel, e.g. to take the ladder rewards which waited for room in the bag.

## Causes the following actions on the server side

The server does it and sends the HnmoGuildsState message with the result.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x13  | Packet header - sub packet type identifier |
| 4 | 1 | HnmoGuildsActionType |  | Action |
| 5 |  | String |  | Argument; Not used yet; empty. |

### HnmoGuildsActionType Enum

The action of the guild levels panel.

| Value | Name | Description |
|-------|------|-------------|
| 1 | ClaimRewards | Take the guild ladder rewards which wait for room in the bag. |