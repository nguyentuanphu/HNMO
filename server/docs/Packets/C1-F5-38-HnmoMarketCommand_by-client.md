# C1 F5 38 - HnmoMarketCommand (by client)

## Is sent when

The player opens the Item Market (F6 or Julia NPC), lists a bag item, buys or cancels a listing, or claims deliveries/payouts.

## Causes the following actions on the server side

The server performs the atomic escrow market action and replies with HnmoMarketState (F5 39).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   59   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x38  | Packet header - sub packet type identifier |
| 4 | 1 | HnmoMarketActionType |  | Action |
| 5 | 1 | Byte |  | Slot; Inventory slot when listing an item. |
| 6 | 1 | Byte |  | Currency; 0=Zen, 1=WCoinC, 2=WCoinP, 3=GoblinPoints. |
| 7 | 4 | IntegerLittleEndian |  | Price; Asking price in the selected currency. |
| 11 | 48 | String |  | ListingId; Target listing identifier when buying or canceling. |

### HnmoMarketActionType Enum

Item market command action.

| Value | Name | Description |
|-------|------|-------------|
| 1 | Request | Request market listings and personal escrow items/payouts. |
| 2 | List | Escrow and list a bag item. |
| 3 | Buy | Atomically purchase a listing. |
| 4 | Cancel | Cancel your own listing and return item to escrow claim. |
| 5 | Claim | Claim bought/returned items into bag and collect pending Zen payouts. |