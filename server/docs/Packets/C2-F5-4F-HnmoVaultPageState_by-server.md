# C2 F5 4F - HnmoVaultPageState (by server)

## Is sent when

Sent after HnmoVaultPageCommand (F5 4E), and when the vault page changes.

## Causes the following actions on the client side

The web client's vault window shows the page buttons, the page in use and the price of one more page.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x4F  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON: enabled, active, unlocked, max, price, coinType, balance, result message (EN/VI by the player's language). |