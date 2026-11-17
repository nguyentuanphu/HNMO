# C1 F5 34 - HnmoLuckyNumberCommand (by client)

## Is sent when

The web game enters the world, opens Daily Lucky Number (L), picks a number, or claims waiting prizes.

## Causes the following actions on the server side

The server updates the player's pick or claims owed rewards and answers with HnmoLuckyNumberState (F5 35).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   9   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x34  | Packet header - sub packet type identifier |
| 4 | 1 | HnmoLuckyNumberActionType |  | Action |
| 5 | 4 | IntegerLittleEndian |  | Number; The picked lucky number when Action is Pick. |

### HnmoLuckyNumberActionType Enum

Daily lucky number action.

| Value | Name | Description |
|-------|------|-------------|
| 1 | Request | Request current lucky number state. |
| 2 | Pick | Pick a lucky number for the active round. |
| 3 | ClaimOwed | Claim jewels waiting for bag space. |