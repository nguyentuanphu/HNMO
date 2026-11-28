# C2 F5 5C - HnmoPentagramState (by server)

## Is sent when

Sent in a Season 9 realm after entering the world, after a pentagram action, or upon HnmoPentagramCommand.

## Causes the following actions on the client side

Updates the web client's pentagram window, the pentagram slot of the inventory and the element and errtels on item tooltips.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x5C  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Plain-ASCII JSON payload: whether the system is on, the worn pentagram with its element and errtel slots, the pentagrams and errtels of the bag by slot, the mix costs and an optional action result. |