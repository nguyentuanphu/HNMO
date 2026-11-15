# C1 F5 16 - HnmoCommunityAction (by client)

## Is sent when

The player clicks a button of HNMO's community window: take the Discord coins, claim the Zalo coins, enter an event's code or take the waiting rewards.

## Causes the following actions on the server side

The server does it and sends the HnmoCommunityState message with the result.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x16  | Packet header - sub packet type identifier |
| 4 | 1 | HnmoCommunityActionType |  | Action |
| 5 |  | String |  | Argument; The event's code (EnterCode), the player's name in Zalo (ClaimZalo, may be empty); empty otherwise. |

### HnmoCommunityActionType Enum

The action of the community window.

| Value | Name | Description |
|-------|------|-------------|
| 1 | ClaimDiscord | Take the one-time coins for linking the account to Discord. |
| 2 | ClaimZalo | Claim the one-time coins for joining the Zalo group; the admin approves the claim. |
| 3 | EnterCode | Enter a community event's code. |
| 4 | ClaimRewards | Take the event rewards which wait for room in the bag. |