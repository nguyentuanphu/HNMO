# C1 F5 10 - HnmoOnboardingAction (by client)

## Is sent when

The player clicks a button of HNMO's guide panel: skip the guide (after a confirmation), skip an optional step, or take the rewards which waited for room in the bag.

## Causes the following actions on the server side

The server does it and sends the HnmoOnboardingState message with the result.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x10  | Packet header - sub packet type identifier |
| 4 | 1 | HnmoOnboardingActionType |  | Action |
| 5 |  | String |  | Argument; Not used yet; empty. |

### HnmoOnboardingActionType Enum

The action of the guide panel.

| Value | Name | Description |
|-------|------|-------------|
| 1 | SkipGuide | End the guide for this character; rewards earned before still come. |
| 2 | SkipStep | Skip the current step, when it's optional (no reward). |
| 3 | ClaimRewards | Take the guide's rewards which wait for room in the bag. |