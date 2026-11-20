# Developer hub: licences in the admin panel, a Developer page, bug reports to GitHub (2026-11-20, Sydney)

## The owner's decisions (pop-ups, 2026-11-20 afternoon)

- Bug reports a server's owner sends on go to **GitHub**: a small web
  function takes them (no token in the game or on any website the hosts see), and an hourly GitHub Action turns them
  into issues, one issue per bug with a count of the servers that saw it, labels by game area, a daily summary.
  Discord stays as an optional notice.
- The issues live in **a private repository of their own** (e.g. `nguyentuanphu/hnmo-hosts`), apart from the code;
  it also keeps the list of hosts and the licences issued.
- **Servers without a licence** (the free 10 players) may send reports too, limited per computer per day, marked
  "no licence". A licensed server is known by its signed `license.hnmo` (no sign-up for hosts).
- **The licence moves into the admin panel** (Host settings): see it, paste or upload it, check its signature at
  once, copy this computer's id, "Ask for a licence"; it is still saved as `license.hnmo` so offline play works.
- **A Developer page** in the admin panel, shown only on the owner's computer (where the private key file is): sign
  and renew licences with a form instead of the command line, the hosts, their reports and requests. The key never
  leaves that computer.
- **A bug robot** (Claude on GitHub Actions): later, once real reports come in.

## Steps

1. Licence in Host settings (view, paste/upload, verify, machine id, saved as `license.hnmo`, applied without a
   restart where possible).
2. Developer page: sign a licence (licensee, players, expiry, machine id) with the local private key; a local list of
   issued licences.
3. The report hub: a web function (in `tools/developer-hub/`, to copy into the private repository and its web site),
   which checks the licence signature or applies the free-server limit and keeps the reports; the hourly GitHub
   Action that files and updates the issues; the admin panel's "Send to the HNMO developer" posts there; "Ask for a
   licence" posts there too.
4. The Developer page reads the hub (reports, licence requests, hosts).
5. Later: the bug robot.

## What the owner sets up (once, step 3)

- A private repository (e.g. `hnmo-hosts`) and a free web site linked to it; the hub's secret key in the site's
  settings and in the repository's Actions secrets. Exact clicks in `tools/developer-hub/README.md`.
