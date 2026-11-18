# C1 F5 4C - HnmoEscrowCommand (by client)

## Is sent when

Sent by the web client to request or mutate escrow contracts or trade credit actions.

## Causes the following actions on the server side

The server validates safety, balance, and concurrency rules and replies with HnmoEscrowState (F5 4D).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x4C  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Action; 0 = RequestState, 1 = CreateContract, 2 = AcceptContract, 3 = CancelContract, 4 = ClaimOwed. |
| 5 |  | String |  | Payload; JSON command payload (contract details, contractId, pin, confirmHighValue). |