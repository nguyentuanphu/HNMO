# C2 F5 0F - HnmoOnboardingState (by server)

## Is sent when

After the client requested HNMO's new-player guide (HnmoOnboardingRequest) or did an action there, and when something changed for a client which requested it before (a step was done or counted, rewards were given).

## Causes the following actions on the client side

The guide panel shows the state; a notice in the chat tells about the event in it (e.g. a step done, the starter kit, a reward).

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC2  | [Packet type](PacketTypes.md) |
| 1 | 2 |    Short   |      | Packet header - length of the packet |
| 3 | 1 |    Byte   | 0xF5  | Packet header - packet type identifier |
| 4 | 1 |    Byte   | 0x0F  | Packet header - sub packet type identifier |
| 5 |  | String |  | Json; The state as JSON, in plain ASCII (other characters are escaped): the status, the steps with their rewards, the current step with its count and where it leads, the waiting rewards, the starter kit when it was just given, and the event which caused it, if any. |