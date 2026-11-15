# C1 F5 19 - HnmoTopRacesAction (by client)

## Is sent when

The player clicks a button of HNMO's top races window, e.g. to take the prizes which waited for room in the bag.

## Causes the following actions on the server side

The server does it and sends the HnmoTopRacesState message with the result.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x19  | Packet header - sub packet type identifier |
| 4 | 1 | HnmoTopRacesActionType |  | Action |
| 5 |  | String |  | Argument; Not used yet; empty. |

### HnmoTopRacesActionType Enum

The action of the top races window.

| Value | Name | Description |
|-------|------|-------------|
| 1 | ClaimRewards | Take the top race prizes which wait for room in the bag. |