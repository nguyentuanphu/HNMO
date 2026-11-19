# C1 F5 4E - HnmoVaultPageCommand (by client)

## Is sent when

Sent by the web client when its vault window opens, when the player picks a vault page, or buys one more page.

## Causes the following actions on the server side

The server answers with HnmoVaultPageState (F5 4F); a switch also sends the new page's items to the open vault window.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   6   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x4E  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Action; 0 = RequestState, 1 = SwitchPage, 2 = BuyPage. |
| 5 | 1 | Byte |  | Page; The page (0-based) for SwitchPage; 0 otherwise. |