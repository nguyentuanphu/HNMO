# C2 F5 4D - HnmoEscrowState (by server)

## Is sent when

Sent when the player requests escrow contracts (F9 / /contract) or completes an escrow operation.

## Causes the following actions on the client side

Updates the Contracts window and reputation/owed summary in the web client.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x4D  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON payload containing contracts, owed deliveries, safety flags, reputation, and notices. |