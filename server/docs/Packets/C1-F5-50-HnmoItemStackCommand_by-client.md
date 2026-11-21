# C1 F5 50 - HnmoItemStackCommand (by client)

## Is sent when

Sent by the web client to ask for the item stack kinds, to split a stack, or to unlock a stack kind with coins.

## Causes the following actions on the server side

The server answers with HnmoItemStackState (F5 51); a split also sends the bag or the open vault again.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x50  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Action; 0 = RequestState, 1 = Split, 2 = Unlock. |
| 5 | 1 | Byte |  | Storage; For Split: 0 = the bag, 1 = the open vault. |
| 6 | 1 | Byte |  | Slot; For Split: the stack's slot. |
| 7 | 1 | Byte |  | Count; For Split: how many pieces to split off. |
| 8 |  | String |  | Kind; For Unlock: the stack kind's key. |