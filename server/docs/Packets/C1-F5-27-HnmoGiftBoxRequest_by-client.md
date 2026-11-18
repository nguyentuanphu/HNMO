# C1 F5 27 - HnmoGiftBoxRequest (by client)

## Is sent when

The signed-in player opens the mailbox window, browses a page/letter or collects a reward.

## Causes the following actions on the server side

The server validates the session, collects safely under the player lock if requested and sends HnmoGiftBoxState.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x27  | Packet header - sub packet type identifier |
| 4 | 4 | IntegerLittleEndian |  | RequestId; A nonzero request correlation identifier; not an authentication token. |
| 8 |  | String |  | Json; ASCII JSON with action, offset, optional letterId and language; at most 247 bytes. |