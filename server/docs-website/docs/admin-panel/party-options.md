# Party options (`/party-options`)

Adds `/partypass` password join and Auto-Party hunting spot listings without changing the 5-member party UI.

## Features
- **Off by default**: enable in `/party-options` (`PartyOptionsPlugIn`).
- **Password join (`/partypass PASS`)**: party leaders (or solo players preparing a party) set a password so friends join immediately by typing `/partypass PASS` or entering the password inside the `P` Party window's Auto-Party drawer. Use `/partypass clear` to stop advertising.
- **Auto-party hunting spots**: advertise a spot label + optional level range; other solo players click **Join** in `P` to join directly (creating the party automatically when a solo leader gets their first joiner).
- **Public view**: active spot listings (without raw passwords) show at `home.html#party-spots` (`/website/party-spots`).
