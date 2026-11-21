# GM Manager & In-Game GM Commands (`/gm`)

GM Manager (ROADMAP 14.17 + 14.11) assigns per-GM command permissions and enforces them centrally when any Game Master runs an in-game GM command.

## Permission Groups

| Flag | Commands Enforced |
| --- | --- |
| **Announce** | `/goldnotice`, `/slidenotice`, `/gmannounce` |
| **Move / Summon / Go To** | `/teleport`, `/trace`, `/goto`, `/track`, `/summon`, `/move`, `/guildmove`, `/gmmove`, `/hide`, `/unhide` |
| **Spawn Monster / Boss** | `/createmonster`, `/spawn`, `/boss`, `/npc`, `/removenpc`, `/killallmonsters`, `/walkmonster`, `/movemonster`, `/fireworks`, `/xmasfireworks`, `/shownpcids` |
| **Start Event** | `/startevent <bc\|cc\|ds\|it\|kanturu>`, `/startbc`, `/startcc`, `/startds`, `/startit`, `/startkanturu`, `/skipminigamewait`, `/crywolf` |
| **Inspect Inventory & Info** | `/inspectinv <char>`, `/charinfo`, `/getlevel`, `/getstat`, `/getmoney`, `/getresets`, `/online` |
| **Mute / Kick / Ban** | `/mute`, `/chatban`, `/chatunban`, `/kick`, `/disconnect`, `/guilddisconnect`, `/banacc`, `/banchar`, `/unbanacc`, `/unbanchar` |
| **Direct Character Edits** | `/setlevel`, `/setstat`, `/setmoney`, `/setresets`, `/item`, `/clearinventory`, `/openwarehouse`, `/pk`, `/pkclear`, `/skin` |

Every allowed or denied GM command attempt is recorded in both the GM Command Audit table and the central Activity Log (`ActivityKind.Administration`).
