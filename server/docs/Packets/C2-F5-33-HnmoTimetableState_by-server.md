# C2 F5 33 - HnmoTimetableState (by server)

## Is sent when

After the client requested the HNMO event and boss timetable (HnmoTimetableCommand).

## Causes the following actions on the client side

The web game updates the Event and Boss Timetable window with Sydney and local times, maps, and drops.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x33  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; The timetable state as JSON, in plain ASCII (other characters are escaped): every event, invasion and boss with category, English and Vietnamese names, map names, drops, next start time in UTC, and Sydney wall-clock schedule. |