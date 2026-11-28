# C1 F5 5D - HnmoElementalHit (by server)

## Is sent when

Sent to an attacker in a Season 9 realm when a hit dealt elemental damage.

## Causes the following actions on the client side

The web client shows the elemental part of the damage above the target, in the element's colour.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   11   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x5D  | Packet header - sub packet type identifier |
| 4 | 2 | ShortBigEndian |  | TargetId; The id of the target object. |
| 6 | 4 | IntegerLittleEndian |  | Damage; The elemental damage of the hit. |
| 10 | 1 | Byte |  | Element; The attacker's element: 1 fire, 2 water, 3 earth, 4 wind, 5 darkness. |