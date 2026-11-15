# C1 F5 1C - HnmoGiftCodesAction (by client)

## Is sent when

The player clicks a button of HNMO's gift code window: enter a code, or take the rewards which waited for room in the bag.

## Causes the following actions on the server side

The server does it and sends the HnmoGiftCodesState message with the result.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x1C  | Packet header - sub packet type identifier |
| 4 | 1 | HnmoGiftCodesActionType |  | Action |
| 5 |  | String |  | Argument; The code for Redeem; empty otherwise. |

### HnmoGiftCodesActionType Enum

The action of the gift code window.

| Value | Name | Description |
|-------|------|-------------|
| 1 | Redeem | Enter the gift code of the argument. |
| 2 | ClaimRewards | Take the gift code rewards which wait for room in the bag. |