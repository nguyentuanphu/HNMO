# C1 F5 32 - HnmoTimetableCommand (by client)

## Is sent when

The web game enters the world or opens the Event and Boss Timetable window.

## Causes the following actions on the server side

The server replies with HnmoTimetableState (F5 33).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   69   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x32  | Packet header - sub packet type identifier |
| 4 | 1 | HnmoTimetableActionType |  | Action |
| 5 | 64 | String |  | Argument; Optional filter or payload string. |

### HnmoTimetableActionType Enum

Timetable action.

| Value | Name | Description |
|-------|------|-------------|
| 1 | Request | Request the full event, invasion and boss timetable. |