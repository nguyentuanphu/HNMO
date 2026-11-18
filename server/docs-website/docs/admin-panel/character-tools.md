# Character Tools (`/character-tools`)

Character Tools (ROADMAP 14.15) provides focused operator actions for a single character while enforcing online/offline safety and audit logging.

## Capabilities

1. **Unstuck / Move to Town** (works online and offline):
   - Teleports a live character or moves an offline character safely to a chosen town's spawn gate / safe tile (or automatically selects the character class's home town).
2. **Live Buffs** (online characters):
   - Applies a configured buff effect with a bounded duration (`10–86400` seconds) or clears active timed buffs on the live character.
3. **Level, Stats, Zen & Class Editor** (offline-only):
   - Changes regular level, master level, resets, Strength, Agility, Vitality, Energy, Leadership, inventory Zen, and character class.
   - **Safety guard:** Refuses edits immediately if the character's account is currently online, and acquires an exclusive login reservation before mutating persistence.
4. **Audit & Activity Log:**
   - Every action requires a reason (`>= 3` characters) and writes both an Admin Audit entry and an Activity Log (`Administration`) record.
