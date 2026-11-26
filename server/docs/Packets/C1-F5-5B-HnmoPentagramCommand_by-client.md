# C1 F5 5B - HnmoPentagramCommand (by client)

## Is sent when

Sent by the web client in a Season 9 realm to open the pentagram window, wear or take off the pentagram, put an errtel into a slot or take it out, or mix an errtel.

## Causes the following actions on the server side

The server performs the action and responds with HnmoPentagramState (F5 5C).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   7   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x5B  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Action; 1 = Request state, 2 = Wear the pentagram of a bag slot, 3 = Take off the pentagram, 4 = Put the errtel of a bag slot into an errtel slot, 5 = Take the errtel of an errtel slot out, 6 = Level up the errtel of a bag slot, 7 = Rank up the errtel of a bag slot with a second one. |
| 5 | 1 | Byte |  | Slot; The bag slot of the pentagram or errtel. |
| 6 | 1 | Byte |  | Index; The errtel slot of the pentagram (0..4), or the bag slot of the second errtel of a rank-up. |