# Audit Findings — `server/src/GameLogic/PlugIns/AntiCheat`

- **Unit**: `server/src/GameLogic/PlugIns/AntiCheat` (5 files, 572 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `AntiCheatAuditStore.cs`, `AntiCheatConfiguration.cs`, `AntiCheatEngine.cs`, `AntiCheatPlugIn.cs`, `AntiCheatViolationEntry.cs`

## Rules & Cross-Check
| Rule | HNMO Implementation | Season 6 / 9 Suite Reference | Status |
| --- | --- | --- | --- |
| Attack speed cadence | `AntiCheatEngine.ComputeMinAttackIntervalMs`: `rawMs = max(FloorMs=70, BaseMs=420 - AttackSpeed * 0.45)`, allowed `= max(FloorMs * 0.7, rawMs * LatencyToleranceFactor=0.7)` (min `49 ms`) | Suite `[AntiHack]`: `EnableAgilityCheck = 0/1` (`7500ms`), client-DLL speed check (`EnablePacketTimeCheck = 1`), `EnableHitHackDetection = 0` (`HitHackMaxAgility = 23000` in S9) | Different enforcement model |
| HitHack Agility cap | `AntiCheatConfiguration.HitHackMaxAgility = 32767` (opt-in via `EnableHitHackAgilityCheck = false`) | S9 `Common` INI: `HitHackMaxAgility = 23000` | Differs (`32767` vs `23000`) |
| Strikes & SafeZone audit | `StrikesBeforeDisconnect = 10` per `10 min` window; `LogSafeZoneAttempts = true` | Suite: `AntiRefCheckTimeMSEC = 500`, `DetectedHackKickCount = 10` | Same kick threshold (`10`) |
| Per-IP connection cap | `MaxConnectionsPerIp = 3` (loopback exempt) | OS / connect-server level in suite | Custom |

## Findings & Observations
1. **Caster Speed Omission — `AntiCheatPlugIn.cs:166`**:
   - Reads only `player.Attributes[Stats.AttackSpeed]`. If a caster class has higher `Stats.MagicSpeed` than physical `Stats.AttackSpeed`, `ComputeMinAttackIntervalMs` may compute a tighter minimum interval than intended for magic spells.
2. **Timestamp Frequency Mismatch — `AntiCheatPlugIn.cs:175`**:
   - Uses `(nowTimestamp - tracker.LastAttackTimestamp) * 1000.0 / Stopwatch.Frequency` instead of `this._timeProvider.GetElapsedTime(...)`; safe with `TimeProvider.System` on Windows/.NET, but would mis-scale if a custom `TimeProvider` defines a different `TimestampFrequency`.

## Re-check 2026-11-27 (changes 8b9574d1..a108e846)

Commits read (every changed file read in full):

- 39443f93 fix(gamelogic): fix anticheat speed/time, season coin race, party master exp gate, chaos jewel count, and gens quest reward

Evidence:

- AntiCheatPlugIn: the speed check uses max(AttackSpeed, MagicSpeed) (casters were judged by attack speed only) and TimeProvider.GetElapsedTime (testable clock) - fixed.
