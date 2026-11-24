# Unit Audit: `server/src/GameLogic/PlugIns/ChatCommands (files 1)`

- **Audit date**: `2026-11-24`
- **Status**: `Checked` (97 files read in full)

## Summary of Rules & Behavior

- **Command Framework**: `IChatCommandPlugIn`, `ChatCommandPlugInBase<T>`, `CommandExtensions`, `ChatCommandArguments`, `ChatCommandTypeExtensions`, `HelpCommand`, `ListCommand`.
- **Player Commands**: `/add`, `/addstr`, `/addagi`, `/addvit`, `/addene`, `/addcmd`, `/clearinv`, `/language`, `/title` (`/title <id|auto>`), `/discord`, `/code`, `/war`, `/battlesoccer`, `/lock`, `/lockitem`, `/move`, `/npc`, `/offlevel`, `/offtrade`, `/openware`, `/partypass`, `/pkclear`, `/post` (`ZenCost = 1_000`, `CooldownSeconds = 5`), `/rank`, `/shopprice`.
- **GM Commands**: `/banacc`, `/banchar`, `/charinfo`, `/chatban`, `/chatunban`, `/createmonster`, `/spawn`, `/crywolf`, `/disconnect`, `/kick`, `/getlevel`, `/getleveluppoints`, `/getmasterlevel`, `/getmasterleveluppoints`, `/getmoney`, `/getresets`, `/get`, `/goto`, `/guilddisconnect`, `/guildmove`, `/hide`, `/inspectinv`, `/item`, `/killall`, `/movemonster`, `/mute`, `/goldnotice`, `/online`, `/pk`, `/removenpc`, `/setlevel`, `/setleveluppoints`, `/setmasterlevel`, `/setmasterleveluppoints`, `/setmoney`, `/setresets`, `/set`, `/fireworks`, `/showids`, `/xmasfireworks`, `/skin`, `/skipwait`, `/slidenotice`, `/startbc`, `/startcc`, `/startds`, `/startevent`, `/startit`, `/startkanturu`, `/summon`, `/teleport`, `/trace`, `/track`, `/unbanacc`.

## Findings / Defects

1. **`CommandExtensions.TryParseArgumentsAsync` ignores `success` on positional arguments (`CommandExtensions.cs:58-67`)**: `success` is updated in the loop (`success = await TrySetPropertyValueAsync(...) && success`), but `return instance;` is returned unconditionally even when `success` is `false` (leaving default/zero values in place).
2. **`BanAccChatCommandPlugIn` / `UnBanAccChatCommandPlugIn` report success on failure**: Neither checks the `bool` return value of `ChangeAccountStateByLoginNameAsync`, so the confirmation message is still shown if the account was not found.
3. **Unclamped coordinate byte addition in `TraceChatCommandPlugIn` / `TrackChatCommandPlugIn`**: `(byte)(PositionX + 2)` and `(byte)(PositionY + 2)` wrap around if the target/GM is at coordinate `254` or `255`.
