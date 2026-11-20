# C1 F5 52 - HnmoArenaTravelRequest (by client)

## Is sent when

Sent by the web client to ask about the HNMO Arena, or for a one-time ticket to travel to the Arena (or from the Arena back home).

## Causes the following actions on the server side

The server answers with HnmoArenaTravelState (F5 53).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   6   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x52  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Action; 0 = RequestState, 1 = Travel (a one-time ticket for the other side), 2 = JoinRankedQueue (in the Arena), 3 = LeaveRankedQueue, 4 = BuyFromPointsShop (at home). |
| 5 | 1 | Byte |  | Index; For BuyFromPointsShop: the item's index in the shop. |