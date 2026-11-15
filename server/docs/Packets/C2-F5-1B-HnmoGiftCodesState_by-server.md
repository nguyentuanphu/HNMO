# C2 F5 1B - HnmoGiftCodesState (by server)

## Is sent when

After the client requested HNMO's gift codes (HnmoGiftCodesRequest) or did an action there, and when something changed for a client which requested them before (a code entered with the chat command, waiting rewards given).

## Causes the following actions on the client side

The gift code window shows the result of the entered code, the rewards waiting for room in the bag and the account's last codes; a notice in the chat tells about the event in it.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x1B  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; The state as JSON, in plain ASCII (other characters are escaped): the waiting rewards, the account's last codes, and the event which caused it, if any (the result, the reward, the tries left). |