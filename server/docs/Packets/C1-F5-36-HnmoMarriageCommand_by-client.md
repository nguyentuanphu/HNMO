# C1 F5 36 - HnmoMarriageCommand (by client)

## Is sent when

The player opens the Marriage window (Shift+M), proposes, accepts/declines, edits couple title, warps to spouse, or divorces.

## Causes the following actions on the server side

The server validates the ceremony/marriage action and sends HnmoMarriageState (F5 37).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC1  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   53   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x36  | Packet header - sub packet type identifier |
| 4 | 1 | HnmoMarriageActionType |  | Action |
| 5 | 48 | String |  | Argument; Partner character name or custom couple title. |

### HnmoMarriageActionType Enum

Marriage action.

| Value | Name | Description |
|-------|------|-------------|
| 1 | Request | Request marriage state. |
| 2 | Propose | Propose marriage to Argument character name. |
| 3 | Accept | Accept pending marriage proposal and hold ceremony at Devias Church. |
| 4 | Decline | Decline pending proposal. |
| 5 | Divorce | Divorce current spouse according to server rules. |
| 6 | SetTitle | Set custom couple title. |
| 7 | TeleportToSpouse | Teleport to online spouse. |