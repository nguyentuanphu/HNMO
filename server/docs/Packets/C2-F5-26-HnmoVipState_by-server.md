# C2 F5 26 - HnmoVipState (by server)

## Is sent when

The server finishes a correlated own-account VIP request.

## Causes the following actions on the client side

The web game updates VIP status, reviewed offers and the localized action result.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x26  | Packet header - sub packet type identifier |
| 5 | 4 | IntegerLittleEndian |  | RequestId; A nonzero request correlation identifier; not an authentication token. |
| 9 |  | String |  | Json; ASCII-safe JSON using the shared VIP account state; Unicode is escaped and no private receipt or credential is included. |