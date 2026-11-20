# Remote admin panel and one player limit (2026-11-21, Sydney)

## Owner decisions (pop-up, 2026-11-21 morning)

- **Player limit:** one licence for the folder, 10 players online at once without one, whatever the season
  (Season 6.3, Season 9 and the Arena together).
- **Admin panel from afar:** the licence from afar; the 6-digit code for signing in from afar; one path for all three
  panels. (Not chosen: the rest of Host settings from afar.)

## Done

- **Shared limit** (`LicensedPlayerLimit`): each program publishes the accounts holding a place in the shared database
  (`data."HnmoLicensedLogin"`, on every login and logout and every 30 s, rows older than 2 minutes don't count). A login
  counts this program's accounts and the others' together; an account already playing elsewhere takes no new place.
  The grace week still applies. The weekly report's numbers are the folder's. Test `PlayerLicenseTests`.
- **Licence from afar** (`HostSettingsPage`): through the website the licence card shows (paste, ask the developer,
  machine id); ports, folders, the database password, Save and Restart only on the host computer. Test
  `HostSettingsPageTests` (both ways).
- **6-digit code from afar** (`AdminLoginService.CheckPasswordAsync(..., fromAfar)`, `Login.razor.cs`,
  `AuthEndpoints`): a sign-in that doesn't come straight from the host computer needs the authenticator; a user without
  one is told to set it up on the host computer (Account security). The sign-in endpoint refuses a ticket from afar
  made without the second factor. Test `AdminAuthenticationTests.FromAfarTheSecondFactorIsRequiredAsync`.
- **One path** (`client/web/bridge/AdminGateway.cs`, `WebsitePrefixMiddleware`): `<admin path>/` is Season 6.3's panel,
  `<admin path>/s9/` Season 9's and `<admin path>/arena/` the Arena's (when they run). The admin users already live in
  the main database for every program and the data-protection keys are shared, so one sign-in opens all three. The
  season chooser links stay under the path (`SeasonPanels.ForWebsite`). Tests `SeasonPanelsTests`, e2e
  `admin-remote.spec.ts` (opt-in `ADMIN_REMOTE=1`: makes an admin user, then signs in from afar with the code, sees the
  licence card and opens the Arena's panel without signing in again; remove the user afterwards on a test save).
