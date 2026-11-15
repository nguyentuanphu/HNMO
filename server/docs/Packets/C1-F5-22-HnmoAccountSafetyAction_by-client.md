# C1 F5 22 - HnmoAccountSafetyAction (by client)

## Is sent when

The player enters the second password in the web game, or locks or unlocks an item.

## Causes the following actions on the server side

The server does it, answers in the chat and sends the HnmoAccountSafetyState message with the result.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   10   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x22  | Packet header - sub packet type identifier |
| 4 | 1 | HnmoAccountSafetyActionType |  | Action |
| 5 | 1 | Boolean |  | Vault; Whether the slot is in the vault (else the inventory). |
| 6 | 1 | Byte |  | Slot; The item's slot. |
| 8 | 2 | ShortLittleEndian |  | Pin; The second password (0-9999); 0xFFFF when none was entered. |

### HnmoAccountSafetyActionType Enum

The action of the account safety.

| Value | Name | Description |
|-------|------|-------------|
| 1 | EnterPin | Enter the second password. |
| 2 | LockItem | Lock the item at the slot. |
| 3 | UnlockItem | Ask the unlock of the item at the slot, with the second password. |