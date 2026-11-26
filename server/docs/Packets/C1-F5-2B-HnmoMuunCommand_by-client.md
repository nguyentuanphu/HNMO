# C1 F5 2B - HnmoMuunCommand (by client)

## Is sent when

Sent by the web client to open the Season 9 Muun window, make a bag Muun the active one, put it away, evolve it, convert a Muun into energy or level up the active Muun.

## Causes the following actions on the server side

The server performs the action and responds with HnmoMuunState (F5 2C).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   6   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x2B  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Action; 1 = Request state, 2 = Make the Muun in the bag slot active, 3 = Put the active Muun away, 4 = Evolve the active Muun, 5 = Convert the Muun in the bag slot into energy, 6 = Level up the active Muun. |
| 5 | 1 | Byte |  | Slot; Inventory slot of the Muun for actions 2 and 5. |