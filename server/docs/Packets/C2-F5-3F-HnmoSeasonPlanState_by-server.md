# C2 F5 3F - HnmoSeasonPlanState (by server)

## Is sent when

Sent after entering the world, when a new season stage unlocks, or upon HnmoSeasonPlanRequest.

## Causes the following actions on the client side

Updates the web client's Season Plan (F8) timeline.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x3F  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON payload containing active stage, unlocked feature gates, and all stage conditions/dates. |