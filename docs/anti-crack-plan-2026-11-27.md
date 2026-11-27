# HNMO anti-crack / licence hardening plan (27 Nov 2026, Sydney)

Owner asked (27 Nov, ~16:55 Sydney): with the licence and its key, can the server still be reverse engineered,
and how do we tighten it and lock out anyone who tries to crack it. This is the plan. Heavy and on-PC work goes to
Antigravity (owner's rule, 27 Nov 11:55).

## The honest frame

The server runs on the renter's own computer. They control that machine fully — memory, files, network. So **no check
that lives inside the server can be made uncrackable**: the check and the thing it protects sit in the same assemblies,
and whoever can read one can patch the other. The realistic goal is **"cheap to run honestly, expensive to crack,
useless once cracked."** The only true lock-out lives on **our hub**, which the renter does not control.

Never build destructive anti-tamper (wiping saves, corrupting data, bricking a machine): legally dangerous, and a
false positive (an antivirus quarantine, a disk glitch) would hit a paying customer. The correct "lock-out" is a
**silent downgrade to the 10-player free tier** — reversible, harmless to data, and it makes the crack worthless.

## What we have today (good base)

- `server/src/Integrity/IntegrityVerifier.cs` — every file's SHA-256 is listed in a manifest signed with our Ed25519
  key; at startup the server re-hashes and compares (`VerifyFolder` / `Verify`). Catches edited files.
- `server/src/Integrity/PlayerLicense.cs` — licence signed with the same Ed25519 key, optional expiry, optional
  machine binding (`MachineId` from the system id). `DefaultMaximumPlayers = 10` without a valid licence.
- `server/src/GameLogic/DeveloperHub.cs` + `server/src/Startup/Program.cs` — the server asks the hub for a licence,
  reports weekly, and the weekly answer can **withdraw** a licence (`Program.cs:251`). This is our lock-out channel —
  it already exists.

The weak seam is the same in all three: ordinary, unobfuscated .NET assemblies, and each check ends in a single
decision a cracker can flip. In `Program.cs:254` the whole limit is one line: `LicensedPlayerLimit.Maximum = …`.

## Attacks a cracker actually uses

1. Decompile the server (free tools; names are intact) and set the limit high, or make the licence check return valid.
2. Swap our embedded public key for their own and sign their own licences.
3. Copy the licensed machine's system id to fake the binding.
4. Firewall the hub so a withdrawal never arrives.

## The plan — five layers, in order of value

### Layer 1 — Hub-gated updates and data (the real lock-out; cannot be patched out). PRIORITY.
Make the things the host actually wants — client/data updates, new seasons, event configs, packs — served by the hub
**only to a licence that verifies server-side** (signature + machine id + not-withdrawn, checked on our hardware).
A cracked copy keeps running the version it has but **freezes**: no updates, no new content, no fixes. Over weeks it
rots. A local patch can't defeat this, because the decision happens on a machine the renter doesn't own.
- Hub work (hnmobug): an authenticated `/api/update-manifest` that returns the current build/pack manifest + signed
  download URLs only for a valid, non-withdrawn licence; verify the licence signature and machine id hub-side.
- Server/updater work: `Updater.cs` already fetches from the hub; route the manifest and pack fetch through the
  licensed endpoint. No licence / withdrawn → no new manifest (keep running the current files, never break).
- Keep storing nothing new about players (hub rule): licence id, machine id, build stamp only.

### Layer 2 — Silent, multi-point downgrade (no single patch removes enforcement)
- When integrity is `FileMismatch`/`SignatureInvalid`, or the hub withdrew, or the hub has been unreachable past the
  grace window (`DeveloperHub.GraceDays = 7`), **drop silently to 10 players** and report the state to the hub.
  No loud "integrity failed" string to grep for.
- Re-derive the limit in several independent places (the login path `LoginAction.cs`, the weekly service, a periodic
  in-game timer, the admin panel), each reading the signed licence + integrity result, so no one patch is enough.
- Fold the running **build stamp** (`HnmoBuildStamp`) into the weekly report; the hub compares it to what it licensed.

### Layer 3 — Make decompiling cost real time
- Add a .NET obfuscator step to `client/web/tools/package-hnmo.sh` for the published server assemblies
  (`HNMO.GameLogic`, `HNMO.Startup`, `HNMO.Persistence`). Scrambles names; raises the bar from minutes to an
  afternoon, and makes the Layer-2 patch hard to locate. Cheap, do it early.
- Native AOT for the startup/server assemblies compiles to machine code and is far harder to read, but the server
  leans on reflection, plug-ins and EF Core — treat AOT as a separate project with its own testing, not a flag.

### Layer 4 — Harden the binding and detect cloning (passive, hub-side)
- Keep the licensee name in every licence (already done) and the first-setup IP + country (`/api/setup`, owner's one
  exception). Add a stable first-run fingerprint stored hub-side; the same licence reporting from two unrelated
  fingerprints/IPs is a leak signal — surface it in the hub, no action on the host.

### Layer 5 — Offline-grace degrade (blocking the hub costs the same as withdrawal)
- Already partly present via `GraceDays`. Make "no hub contact for N days" degrade to the free tier the same way a
  withdrawal does. Silence then buys the cracker nothing.

## Legal cleanup (do alongside)
- The MIT headers on our own files (`PlayerLicense.cs`, `IntegrityVerifier.cs`, `DeveloperHub.cs`, …) legally let
  anyone modify and redistribute our licence check. Keep MIT only on third-party code; put our own terms on code we
  wrote. This is a header/licence-file change, not a code change — needs an owner decision on the exact terms.

## Split of work
- **Antigravity (heavy / on-PC):** Layer 1 (hub endpoint + updater routing, needs the hub and a real package run),
  Layer 3 (obfuscator in the packager + a real packaged-folder launch to prove it still starts), Layer 2 server code
  with tests. One draft PR per layer, server tests + the touched tests, into the working branch (hub work into the
  hnmobug branch).
- **Main cloud Claude:** review and merge; draft the hub API shape; keep this doc and the owner-questions list current.
- **Owner decisions needed:** (a) go-ahead on Layer 1 as the priority; (b) the licence terms to replace MIT on our own
  files; (c) whether to pay for a specific obfuscator or use a free one.

## Reality check
None of this makes the server uncrackable. Layer 1 is what actually protects revenue: a cracked copy still runs, but
it never gets anything new and is traceable to who leaked it. Everything else raises the cost of the initial crack.
