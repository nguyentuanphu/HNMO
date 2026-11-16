# C1 F5 3A - HnmoJewelBankCommand (by client)

## Is sent when

Sent by the web client to open the Jewel Bank, deposit or withdraw jewels, or clear master skill points.

## Causes the following actions on the server side

The server performs the action and responds with HnmoJewelBankState (F5 3B).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x3A  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Action; 1 = Request state, 2 = Deposit single bag slot, 3 = Deposit all jewels, 4 = Withdraw jewel, 5 = Clear master skill points. |
| 5 | 1 | Byte |  | Slot; Bag slot index when depositing a single slot. |
| 6 | 4 | IntegerLittleEndian |  | Amount; Withdrawal quantity. |
| 10 |  | String |  | Key; Jewel key (e.g. bless, soul, life, creation, chaos, harmony, guardian). |