# HNMO Shared Workboard (Australia/Sydney)

> Updated: 2026-11-19 11:12 +11:00  
> Read this file before starting new work so multiple chats/sessions stay aligned and never duplicate work.

---

## Owner Directives & Deprioritized Scope

- **Deprioritized (DO NOT WORK ON NOW)**: Anything relating to **Linux** (`ROADMAP` §18 Linux VPS deployment kit, `linux-x64` package builds, WSL/Ubuntu/Debian scripts) — *owner 2026-11-19: "deprioritise this as i dont need for now"*. Focus exclusively on **Windows (`win-x64`), Web Client, AdminPanel, and GameServer (Season 6.3 web-first)**.
- **Client Strategy Question Answered (2026-11-19)**: One combined S6 + S9 desktop client is technically possible via `ClientVersion` profile/packet adapters + separate S6 vs. S9 asset packs, **however** S9 still requires BMD v14+, maps, Pentagram/Errtel, Muun, Extended Master Tree, and GFx UI assets that HNMO does not ship yet, AND the repo currently remains **WEB ONLY** (`ROADMAP` §17.0). Keep S6.3 complete first before ever adding an optional S9 pack/profile.

---

## Completed & Pushed Integration Units

| Branch / PR | Tip | Status | Verification Summary |
|---|---|---|---|
| `antigravity-integration` (Draft PR `#51`) | `650c0431` | **Verified & Pushed** | Full Release build + tests; clean Windows `HNMO-pkg` (`14/14` Playwright + `2/2` `mobile-hud`). Report: [playtest-2026-11-19.md](playtest-2026-11-19.md). |
| `chatgpt-integration` (PR `#26`) | `c02e6a97` | **Verified & Pushed** | Rebuilt atop `antigravity-integration` combining `#2–#29`, `#33`, `#39`, `#62`, `#64`, `#79–#83` + audit fixes (`HNMO.Web.Tests` `1357/1357`, `HNMO-pkg` smoke `17/17`, dev `control` + `vip-servers` `3/3`; 3 German Discord tests blocked on Windows host by Application Control `0x800711C7` on `de/HNMO.Discord.resources.dll`). Report: [audit-chatgpt-2026-11-19.md](audit-chatgpt-2026-11-19.md). |

---

## Active Work Queue (`antigravity-s6-work`, branched from `chatgpt-integration` `c02e6a97`)

**Active Owner/Chat Claim**: Current Antigravity Windows session (`antigravity-s6-work`).

### Wave 1 — Live Setup, Control, Portal & E2E Stability (IN PROGRESS)
1. [x] **Setup Wizard live port sync (`ControlEndpoints.cs`, `Portal.cs`, `Program.cs`)**: Update in-memory `LauncherOptions` and `PortalOptions` (`ConnectServerPort`, `DatabasePort`, `AdminUrl`, `OpenBrowserUrl`) immediately when `/api/control/setup` or restore writes `hnmo-settings.json`, plus 2s HTTP timeout on `ProbeHttpAsync` so `adminPanel` probe handles cold Blazor SSR startup cleanly.
2. [~] **`/control` first-run admin/key UX & `/worlds` table layout**: Added `flex-nowrap text-nowrap` on `/worlds` Start/Stop/Edit buttons at `1280px`; still queued: unified first-run admin/key role guidance and world-qualified server picker labels when sharing server IDs.
3. [x] **`inventory.spec.ts:50` helm-swap assertion**: Polled slot `2` removal cleanly via `expect.poll` alongside `free.slot` addition so `inventory.spec.ts` does not race between packet updates.

### Wave 2 — Remaining Season 6 `/import-server` & Feature-Gap Completion
4. [ ] **`/import-server` S6 Extensions**: Weighted multi-pool Item Bag XML rules, Ancient Set drop rules, and multi-slot map/day bonus schedules (dry-run + backup + undo).
5. [ ] **Remaining S6 Gameplay & Web UI Gaps** ([feature-gap-2026-11-18.md](feature-gap-2026-11-18.md)):
   - Gens strife-map PvP/move locks + chat/nameplate icons.
   - Weighted Item Bags + timed `PeriodTime` expiry + Cancel Item Sale / Item Recovery NPC + Cash Shop bundles / `MonsterGP`.
   - Scheduled invasions (Moon Rabbit / Ring Attack / Snake Egg multi-wave XML rules).
   - Chaos Card lottery mix tiers & Lucky Item durability decay/repair.
   - Guild War & Battle Soccer web UI scoreboards + full Master Skill tree parity outside Rage Fighter.
   - Combat helper AI bots (`BK_Helper` / `SM_Helper`).

### Wave 3 — Code Hygiene & Full Windows E2E Sweep (No Linux)
6. [ ] **Pre-existing C# analyzer cleanup** (`~404` SA/CS warnings in legacy files) + **full non-smoke Windows E2E sweep** across merged PR features (`escrow-contracts`, `world-editor`, `s6-events`, `anti-cheat`, `rate-tuning`, `qol-commands`, `market`, `jewel-bank`, `top-races`) and clean `HNMO-pkg` `win-x64` re-playtest.
