# C2 F5 37 - HnmoMarriageState (by server)

## Is sent when

After any marriage command or when marital/proposal/couple bonus status changes.

## Causes the following actions on the client side

The web game updates the Marriage window (Shift+M) and displays ceremony/proposal notices.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x37  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON containing marital state, couple title, party bonus status, ceremony location, divorce rules, and public couples summary. |