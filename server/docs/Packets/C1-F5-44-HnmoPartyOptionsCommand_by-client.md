# C1 F5 44 - HnmoPartyOptionsCommand (by client)

## Is sent when

Sent by the web client to request party options, advertise a hunting spot party/password, or auto-join a spot/password party.

## Causes the following actions on the server side

The server processes the request and replies with HnmoPartyOptionsState (F5 45).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x44  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Action; 0 = RequestState, 1 = AdvertiseSpotOrPass, 2 = JoinByPassword, 3 = JoinSpot, 4 = StopAdvertising. |
| 5 |  | String |  | Payload; JSON string containing password, spot, leader, minLevel, maxLevel. |