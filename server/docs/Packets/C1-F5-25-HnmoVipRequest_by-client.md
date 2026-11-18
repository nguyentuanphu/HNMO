# C1 F5 25 - HnmoVipRequest (by client)

## Is sent when

The signed-in player opens the VIP window, confirms an offer or claims daily coins.

## Causes the following actions on the server side

The server validates the session and sends HnmoVipState without accepting client prices or account identity.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x25  | Packet header - sub packet type identifier |
| 4 | 4 | IntegerLittleEndian |  | RequestId; A nonzero request correlation identifier; not an authentication token. |
| 8 |  | String |  | Json; ASCII JSON with action, optional operationId, levelId, days and offerToken; at most 247 bytes. |