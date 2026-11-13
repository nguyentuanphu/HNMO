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
