# Crash Watch & Auto-Restart (`/control`, `/crash-watch`)

Monitors the launched game server process in `/control` (`GameLauncher`) and automatically restarts it after unexpected crashes while surfacing notices to the host.

- **Policy (`CrashWatchPolicy`)**: Ignores intentional stops (`/control` Stop / host exit) and configured settings-restart exit codes; applies escalating backoff (`CrashRestartDelaySeconds * min(4, crashesLastHour)`) capped by `MaxCrashRestartsPerHour` so crash loops halt safely with a manual intervention notice.
- **Incident Log (`CrashWatchStore`)**: Saves up to 50 incidents in `save/crash-incidents.json` with Sydney local timestamps, exit codes, and trailing `server.log` lines.
- **Admin Notices**: Unread crash banners appear on both `/control` (EN/VI + Dismiss) and the Admin Panel `/crash-watch` page, plus an optional masked Discord webhook alert.
