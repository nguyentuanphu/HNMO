# C2 F5 29 - HnmoBanNotice (by server)

## Is sent when

Sent when a login, character selection, or chat attempt is blocked by an active account, character, network, device ban or mute.

## Causes the following actions on the client side

The client displays the bilingual EN/VI ban or mute reason and Sydney expiration time.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x29  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; UTF-8 JSON payload containing kind, reasonEn, reasonVi, expiresAtUtc, sydneyEn, and sydneyVi. |