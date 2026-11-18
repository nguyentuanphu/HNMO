# C2 F5 31 - HnmoTopUpMilestonesState (by server)

## Is sent when

After HnmoTopUpMilestonesCommand or when a top-up changes the account's milestone progress.

## Causes the following actions on the client side

The client renders the top-up milestones window and shows any notice.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x31  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; The state as JSON in plain ASCII: enabled, totalVnd, steps, owed rewards, and optional result message. |