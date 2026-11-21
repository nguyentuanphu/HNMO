# C2 F5 54 - HnmoBugReportCommand (by client)

## Is sent when

Sent by the web client when the player opens the bug report window or sends a bug report.

## Causes the following actions on the server side

The server keeps the report for the server's owner and answers with HnmoBugReportState (F5 55).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x54  | Packet header - sub packet type identifier |
| 5 | 1 | Byte |  | Action; 0 = RequestState, 1 = Send. |
| 6 |  | String |  | Json; For Send: plain-ASCII JSON with category, text and info (what the game adds: version, browser, device, recent errors); empty otherwise. |