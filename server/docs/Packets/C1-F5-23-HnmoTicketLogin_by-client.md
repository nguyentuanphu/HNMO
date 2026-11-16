# C1 F5 23 - HnmoTicketLogin (by client)

## Is sent when

The web game connects with a one-time login ticket issued on the website.

## Causes the following actions on the server side

The server redeems the ticket once and logs the player in without a password.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   52   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x23  | Packet header - sub packet type identifier |
| 4 | 10 | String |  | Username; The account login name. |
| 14 | 38 | String |  | Ticket; The one-time login ticket issued on the website. |