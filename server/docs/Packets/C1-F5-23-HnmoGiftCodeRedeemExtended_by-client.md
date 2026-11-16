# C1 F5 23 - HnmoGiftCodeRedeemExtended (by client)

## Is sent when

A web player enters a gift code, including the persistent browser device UUID.

## Causes the following actions on the server side

The server checks the account, network and optional device limits and sends HnmoGiftCodesState with the result.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   72   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x23  | Packet header - sub packet type identifier |
| 4 | 32 | String |  | GiftCode |
| 36 | 36 | String |  | DeviceId |