# C1 F5 30 - HnmoTopUpMilestonesCommand (by client)

## Is sent when

The web game enters the world, opens the top-up milestones window, claims a milestone step, or takes waiting rewards.

## Causes the following actions on the server side

The server performs the action and replies with HnmoTopUpMilestonesState.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x30  | Packet header - sub packet type identifier |
| 4 | 1 | HnmoTopUpMilestonesActionType |  | Action |
| 5 |  | String |  | Argument; Milestone step id when Claim; empty otherwise. |

### HnmoTopUpMilestonesActionType Enum

The action for HNMO top-up milestones.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Request | Request the current top-up milestones state. |
| 1 | Claim | Claim the milestone step identified by Argument. |
| 2 | TakeOwed | Take rewards waiting for bag space. |