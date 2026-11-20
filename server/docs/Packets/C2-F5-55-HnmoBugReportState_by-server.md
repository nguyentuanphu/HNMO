# C2 F5 55 - HnmoBugReportState (by server)

## Is sent when

Sent after HnmoBugReportCommand (F5 54).

## Causes the following actions on the client side

The web client shows the bug report window: reports left today, the categories, the player's reports with their status and answers, and the result of a send.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x55  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON: left, maxLength, categories, reward, mine (id, category, text, status, duplicateOf, reply, sent, reward), message, sent. |