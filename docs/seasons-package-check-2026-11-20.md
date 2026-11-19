# Seasons step 5: packaged check on Linux (2026-11-20, Sydney)

Checked in the cloud container. Windows is the local Claude's (Job 8).

## What was packaged

- Branch `seasons-admin` (PR #98, which contains #97), built with
  `client/web/tools/package-hnmo.sh "$PWD/server" linux-x64 <scratch>/HNMO-pkg`.
- Its `server/`, `web/` and `start-hnmo.sh` replaced those of an existing test folder. The test folder kept its
  `save/`, `Data/` and `Data_S9/`.
- `save/hnmo-settings.json`: `SeasonNineEnabled: true` (the switch on the Host settings page).
- Started once, as a normal user: `./start-hnmo.sh`.

## What was seen

| Check | Result |
|---|---|
| Launcher log | "Season 9 is starting (connect server 44506, admin panel http://127.0.0.1:8096)", then "HNMO is ready" |
| Logs | `save/logs/server.log` and `save/logs/server-s9.log` |
| Season 9 database | `hnmo_s9` set up on its first start; its servers moved 100 ports up (44505/44506, 46001-46006) |
| `/data/seasons.json` | `{"s6":true,"s9":true,"connect":{"s6":44406,"s9":44506}}` |
| Start screen | 3 Season 6.3 servers and 3 Season 9 servers in their columns |
| Season 9 server | test4 picked one and connected to 44506 then 46002. Lorencia, Season 9's characters, no page errors |
| Season 6.3 server | test3 picked one and connected to 45902. Lorencia, no page errors |
| Save files | Season 9's state in `save/s9/hnmo-state.json`; Season 6.3's in `save/` |
| Admin panels | 8095 (Season 6.3) and 8096 (Season 9). Both show the season badge and the three big buttons (`docs/screenshots/admin-seasons-*`) |

Checked earlier on the same database (PR #97):
- a Season 6.3 password logs in on Season 9;
- Season 6.3 coins show in Season 9;
- a top-up made during play arrives at the next save;
- a website ticket works once on a Season 9 server.

## Not checked

- Windows (`Start HNMO.bat`): local Claude, Job 8.
- Season 9's own content: `hnmo_s9` is still a copy of Season 6.3's data until the import (Job 8).
- Stopping HNMO from the control page with both seasons running. The launcher's stop path kills Season 9 too, but
  that was only seen when the processes were killed by hand.
