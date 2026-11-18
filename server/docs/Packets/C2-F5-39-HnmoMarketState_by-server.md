# C2 F5 39 - HnmoMarketState (by server)

## Is sent when

After any Item Market action or when listings/payouts change.

## Causes the following actions on the client side

The web game updates the Item Market window (F6) and posts transaction notices.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x39  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON containing active listings, listable bag items, waiting deliveries/payouts, currency rules, and fee percent. |