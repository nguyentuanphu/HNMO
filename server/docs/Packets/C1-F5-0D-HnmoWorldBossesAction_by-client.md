# C1 F5 0D - HnmoWorldBossesAction (by client)

## Is sent when

The player clicks a button of HNMO's world boss window, e.g. to take the rewards which waited for room in the bag.

## Causes the following actions on the server side

The server does it and sends the HnmoWorldBossesState message with the result.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x0D  | Packet header - sub packet type identifier |
| 4 | 1 | HnmoWorldBossesActionType |  | Action |
| 5 |  | String |  | Argument; Not used yet; empty. |

### HnmoWorldBossesActionType Enum

The action of the world boss window.

| Value | Name | Description |
|-------|------|-------------|
| 1 | ClaimRewards | Take the world boss rewards which wait for room in the bag. |