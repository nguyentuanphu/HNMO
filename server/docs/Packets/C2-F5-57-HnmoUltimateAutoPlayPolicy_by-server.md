# C2 F5 57 - HnmoUltimateAutoPlayPolicy (by server)

## Is sent when

Sent on entering the game and after HnmoUltimateAutoPlayRequest (F5 56).

## Causes the following actions on the client side

The web client enables or VIP-gates Ultimate Autoplay features according to server configuration and Stats.IsVip.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x57  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON: v, enabled, isVip, features (sparkleGuide, manaSdPotions, jewelDeposit, targetPriority, patrol, survival, bossFocus, presetsStats). |