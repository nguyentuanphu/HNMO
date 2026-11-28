# C2 F5 60 - HnmoAutoErrorReport (by client)

## Is sent when

Sent by the web client on its own when the page meets an error: an uncaught error, an unhandled promise rejection, a packet its handlers failed on, a lost WebGL context or an unexpected disconnect (each kind of error once per page load, at most 5 an hour per browser).

## Causes the following actions on the server side

The server groups it by its fingerprint for the server's owner (admin panel, Bug reports, Automatic); no answer.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x60  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; Flat plain-ASCII JSON of strings, no terminator from the web client (the C# writer adds one NUL; both are read): fingerprint (kind|file|line), kind, version, browser, os. Older clients' message, stack, device, map and gpu are accepted and dropped; any other field, a repeated field or more than 16 KiB rejects the packet. Never the login, ticket, password or chat text. |