# Browser tests

They play the web game in Chromium against a running server and web client, like a player:
log in, enter the world, open the inventory, move and equip items, open the vault, hover items.

```bash
# a server, e.g. the demo server (data in memory):
dotnet run --project ../../server/src/Startup -c Release -- -demo -autostart -resolveIP:loopback
# the web client on port 8081:
npm start
# then:
npm run test:e2e                         # or BASE_URL=http://127.0.0.1:8080 for an HNMO folder
```

The "Web game tests" workflow runs them on every push. Each test logs in with its own test
account, because an account stays logged in for a while after a browser closes.

## Arena battles (long, opt-in)

`arena-teams.spec.ts`, `arena-royale.spec.ts` (set `ARENA_BATTLES=1`) and `arena-guildwar.spec.ts` (set
`ARENA_GUILD_WAR=1`) play whole Arena battles with 5-7 browsers (10-25 minutes each). They need the Arena program next
to the web client (`HNMO_SEASON=arena`, bridge with `--Launcher:ArenaEnabled=true`); the guild war also needs two guilds
in the Season 6.3 save and a lower `GuildWarMinMembers` (see docs/plans/arena-2026-11-20.md). CI skips them.
