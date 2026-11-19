# HNMO Shared Workboard (Australia/Sydney)

> Updated: 2026-11-19 12:55 +11:00  
> Read this file before starting new work so multiple chats/sessions stay aligned and never duplicate work.

---

## Owner Directives & Deprioritized Scope

- **Deprioritized (DO NOT WORK ON NOW)**: Anything relating to **Linux** (`ROADMAP` §18 Linux VPS deployment kit, `linux-x64` package builds, WSL/Ubuntu/Debian scripts) — *owner 2026-11-19: "deprioritise this as i dont need for now"*. Focus exclusively on **Windows (`win-x64`), Web Client, AdminPanel, and GameServer (Season 6.3 web-first)**.
- **Client Strategy Question Answered (2026-11-19)**: One combined S6 + S9 desktop client is technically possible via `ClientVersion` profile/packet adapters + separate S6 vs. S9 asset packs — **now unblocked on web assets** via [s9-asset-spec-2026-11-19.md](s9-asset-spec-2026-11-19.md), while keeping **WEB ONLY** (`ROADMAP` §17.0) and keeping S6.3 complete first.

---

## Completed & Pushed Integration Units

| Branch / PR | Tip | Status | Verification Summary |
|---|---|---|---|
| `antigravity-integration` (Draft PR `#51`) | `650c0431` | **Verified & Pushed** | Full Release build + tests; clean Windows `HNMO-pkg` (`14/14` Playwright + `2/2` `mobile-hud`). Report: [playtest-2026-11-19.md](playtest-2026-11-19.md). |
| `chatgpt-integration` (PR `#26`) | `c02e6a97` | **Verified & Pushed** | Rebuilt atop `antigravity-integration` combining `#2–#29`, `#33`, `#39`, `#62`, `#64`, `#79–#83` + audit fixes (`HNMO.Web.Tests` `1357/1357`, `HNMO-pkg` smoke `17/17`, dev `control` + `vip-servers` `3/3`; 3 German Discord tests blocked on Windows host by Application Control `0x800711C7` on `de/HNMO.Discord.resources.dll`). Report: [audit-chatgpt-2026-11-19.md](audit-chatgpt-2026-11-19.md). |

---

## Active Work Queue (`antigravity-s6-work`, branched from `chatgpt-integration` `c02e6a97`)

### Ownership Split (2026-11-19 — Quota & Environment Optimization)
- **Cloud Claude (owns pure C#/TS coding from GitHub)**: Pull `antigravity-s6-work`, claim items below by editing this file, work on **Wave 2**, **Wave 3** (`HNMO.sln` analyzer cleanup & cross-platform `.NET` tests), and **Wave 4B–4D** (`S9` server mechanics, `F5` packets, TypeScript web UIs, AdminPanel pages). Leave `[ ]` -> `[~]`/`[x]` updates in git as you go.
- **Local Antigravity on Windows (reserved for Windows-only / local-disk work)**: **Wave 4A DONE** — inspected extracted local S9 client `Data/` (`D:\MuOnline\IGCN\IGCN x9` outside git), published [s9-asset-spec-2026-11-19.md](s9-asset-spec-2026-11-19.md), and implemented S9 decoders + `Data_S9` overlay. Remaining Antigravity Windows-only work later: `package-hnmo.sh win-x64` into `HNMO-pkg` and Windows Playwright playtests against `HNMO-pkg`.

### Wave 1 — Live Setup, Control, Portal & E2E Stability (COMPLETED by Antigravity)
1. [x] **Setup Wizard live port sync (`ControlEndpoints.cs`, `Portal.cs`, `Program.cs`)**: Update in-memory `LauncherOptions` and `PortalOptions` (`ConnectServerPort`, `DatabasePort`, `AdminUrl`, `OpenBrowserUrl`) immediately when `/api/control/setup` or restore writes `hnmo-settings.json`, plus 2s HTTP timeout on `ProbeHttpAsync` so `adminPanel` probe handles cold Blazor SSR startup cleanly.
2. [x] **`/worlds` button layout & `/api/website` multi-world ServerId safety**: Added `flex-nowrap text-nowrap` on `/worlds` Start/Stop/Edit buttons at `1280px`, and guarded `WebsiteController.Get()` with `GroupBy(x => x.ServerId)` so duplicate server IDs across worlds never throw `ArgumentException`.
3. [x] **`inventory.spec.ts:50` helm-swap assertion**: Polled slot `2` removal cleanly via `expect.poll` alongside `free.slot` addition so `inventory.spec.ts` does not race between packet updates.
4. [x] **First-run `/control` setup clarity**: Added bilingual EN/VI `wizardHint` on `control.html` noting `/users` admin creation (`>= 12` char password) and `Administrator` API key role for Website Portal registration.

### Wave 2 — Remaining Season 6 `/import-server` & Feature-Gap Completion (ASSIGNED: Cloud Claude)
5. [ ] **`/import-server` S6 Extensions**: Weighted multi-pool Item Bag XML rules, Ancient Set drop rules, and multi-slot map/day bonus schedules (dry-run + backup + undo).
6. [ ] **Remaining S6 Gameplay & Web UI Gaps** ([feature-gap-2026-11-18.md](feature-gap-2026-11-18.md)):
   - Gens strife-map PvP/move locks + chat/nameplate icons.
   - Weighted Item Bags + timed `PeriodTime` expiry + Cancel Item Sale / Item Recovery NPC + Cash Shop bundles / `MonsterGP`.
   - Scheduled invasions (Moon Rabbit / Ring Attack / Snake Egg multi-wave XML rules).
   - Chaos Card lottery mix tiers & Lucky Item durability decay/repair.
   - Guild War & Battle Soccer web UI scoreboards + full Master Skill tree parity outside Rage Fighter.
   - Combat helper AI bots (`BK_Helper` / `SM_Helper`).

### Wave 3 — Code Hygiene & Full Windows E2E Sweep (SPLIT: Cloud Claude C# cleanup; Antigravity Windows E2E)
7. [ ] **Cloud Claude**: Clean pre-existing `~404` C# SA/CS analyzer warnings across `HNMO.sln` + run `HNMO.Web.Tests` & `.NET` tests on Linux/CI; **Local Antigravity**: run full non-smoke Windows Playwright sweep (`escrow-contracts`, `world-editor`, `s6-events`, `anti-cheat`, `rate-tuning`, `qol-commands`, `market`, `jewel-bank`, `top-races`) + clean `HNMO-pkg` `win-x64` re-playtest.

### Wave 4 — Season 9 Support on Web App + Server (SPLIT: Antigravity Local Inspection -> Cloud Claude Implementation)
> **Scope**: Stay **WEB ONLY** (`client/web` + `server/` + Windows `HNMO-pkg`), no desktop client. Keep Season 6.3 intact by default and add version-switched Season 9 support backed by an optional extracted S9 `Data` overlay pack (never committing third-party proprietary files/archives from `D:\MuOnline\IGCN\IGCN x9` to git).
> **F5 Packet Budget**: Free `2B–2F`, `4E–4F`, `50–6F`. Already taken: `23` (#12), `24` (#13), `25/26` (#39), `27/28` (#64), `29` (#79), `2A` (#82), `30–4D`.

8. [x] **Wave 4A — S9 Asset Verification, Overlay Pack & Web Decoders (`modulusCipher.ts`, `formats.ts`, `GameFiles.cs`, `Program.cs`, `package-hnmo.sh`) (COMPLETED by Antigravity)**:
   - Published full S9 asset format spec & map/asset diff in [s9-asset-spec-2026-11-19.md](s9-asset-spec-2026-11-19.md).
   - Added pure-TS `client/web/src/modulusCipher.ts` (all 8 Crypto++ ciphers: TEA, ThreeWay, CAST128, RC5, RC6, MARS, IDEA, GOST) and extended `client/web/src/formats.ts` for `BMD` `0x0e`, `"MAP\x01"`, `"ATT\x01"`, and v1 `.obj` (`32`-byte stride). Verified against synthetic unit tests (`4/4`) + all `3,578` S9 `BMD:0x0e` models and all 8 S9 worlds (`83, 92, 94, 95, 96, 99, 101, 103`).
   - Added `.png` -> `.ozp` unwrap (`4`-byte strip) + optional `Data_S9` / `HNMO:S9DataPath` overlay in `GameFiles.cs` and `Program.cs`, plus optional `HNMO_S9_DATA_DIR` overlay hook in `package-hnmo.sh`.
9. [ ] **Wave 4B — Elemental Combat, Pentagram & Errtel (`GameLogic`, Packets, Web UI)**:
   - Implement `GameVersion.Season9Episode2` attribute & elemental damage/defense formulas (`Fire/Water/Earth/Wind/Darkness`), Pentagram equipment slot, Errtel socketing/leveling/combining, AdminPanel `/pentagrams` config, and TypeScript UI panel.
10. [ ] **Wave 4C — Muun Pets, Extended Master Tree (330+) & Stat Specialization**:
    - Implement Muun inventory/slots, timed bonus stats, Evolution Stone fusion, Extended Master Skill Tree (`330+` nodes & S9 spells), and Stat Specialization bonus tiers + TypeScript UI.
11. [ ] **Wave 4D — S9 Maps & Events (`Acheron`, `Arca Battle`, `Tormented Square`, `Chaos Castle Survival`, `Illusion Temple Renewal`, `BattleCore`)**:
    - Implement Acheron & Acheron Guardian, Arca Battle guild obelisk war + Conqueror buffs, Tormented Square (`map 102`), Illusion Temple Renewal, Chaos Castle Survival, and BattleCore cross-realm channel + bilingual EN/VI web HUD/AdminPanel integration + Windows Playwright & `HNMO-pkg` verification.
