# C1 F5 1F - HnmoResetsAction (by client)

## Is sent when

The player asks the web game for a reset, a relife, the next reset's needs, or switches the delegated reset.

## Causes the following actions on the server side

The server does it and sends the HnmoResetsState message with the result (and a chat message).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   5   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x1F  | Packet header - sub packet type identifier |
| 4 | 1 | HnmoResetsActionType |  | Action |

### HnmoResetsActionType Enum

The action of the reset options.

| Value | Name | Description |
|-------|------|-------------|
| 1 | Reset | Reset the character. |
| 2 | Relife | Do the relife (grand reset). |
| 3 | DelegatedOn | Switch the delegated reset on. |
| 4 | DelegatedOff | Switch the delegated reset off. |
| 5 | Info | Tell what the next reset and relife need. |