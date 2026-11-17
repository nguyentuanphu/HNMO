# C1 F5 40 - HnmoLeftoversCommand (by client)

## Is sent when

Sent by the web client to open Combining Leftovers / Exchange NPC (F10), run a combine recipe, or exchange surplus materials.

## Causes the following actions on the server side

The server performs the recipe/exchange and sends HnmoLeftoversState (F5 41).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x40  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Action; 1 = RequestState, 2 = CombineRecipe, 3 = ExchangeOffer. |
| 5 |  | String |  | Id; Target recipe or exchange offer identifier. |