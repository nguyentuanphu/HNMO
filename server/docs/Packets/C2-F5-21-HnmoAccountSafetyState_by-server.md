# C2 F5 21 - HnmoAccountSafetyState (by server)

## Is sent when

After the client requested HNMO's account safety (HnmoAccountSafetyRequest) or did an action there, and when something changed for a client which requested it before (an action needs the second password, an item was locked or unlocked, a locked item moved).

## Causes the following actions on the client side

The web game asks for the second password when an action needs it, marks the locked items and updates the vault's lock.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x21  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; The state as JSON, in plain ASCII (other characters are escaped): whether the account has a second password and entered it, which actions need it, the lockout, the locked items by slot, and the event which caused it, if any. |