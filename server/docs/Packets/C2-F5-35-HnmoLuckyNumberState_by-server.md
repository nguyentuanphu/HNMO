# C2 F5 35 - HnmoLuckyNumberState (by server)

## Is sent when

After the client requests the Daily Lucky Number state, picks a number, claims owed rewards, or when the daily draw runs.

## Causes the following actions on the client side

The web game updates the Daily Lucky Number window (L) and posts draw/win chat notices.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x35  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON containing round date, next draw UTC timestamp, number range, odds, prizes, account picks, owed rewards, recent draw results, and event status. |