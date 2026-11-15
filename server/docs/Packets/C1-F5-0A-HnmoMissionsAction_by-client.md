# C1 F5 0A - HnmoMissionsAction (by client)

## Is sent when

The player clicks a button of HNMO's missions window: claim the reward of a mission, the streak bonus or the login calendar day, or enter who invited the account.

## Causes the following actions on the server side

The server does it and sends the HnmoMissionsState message with the result.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |      | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x0A  | Packet header - sub packet type identifier |
| 4 | 1 | HnmoMissionsActionType |  | Action |
| 5 |  | String |  | Argument; The mission id for ClaimMission; the invite code or a character name of the inviting account for EnterInvite; empty otherwise. |

### HnmoMissionsActionType Enum

The action of the missions window.

| Value | Name | Description |
|-------|------|-------------|
| 1 | ClaimMission | Claim the reward of a finished mission. |
| 2 | ClaimStreak | Claim the bonus of the daily streak. |
| 3 | ClaimCalendar | Claim today's day of the login calendar. |
| 4 | EnterInvite | Enter who invited the account. |