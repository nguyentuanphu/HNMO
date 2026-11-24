# Audit: `server/src/GameLogic/PlugIns/GameplayRules`

- **Unit**: `server/src/GameLogic/PlugIns/GameplayRules`
- **Files read in full**: 25 of 25 (2,082 lines total)
  - `BonusEventEntry.cs` (55), `BonusEventsConfiguration.cs` (26), `BonusEventsPlugIn.cs` (309)
  - `ChaosRatesConfiguration.cs` (23), `ChaosRatesPlugIn.cs` (79), `ChaosRecipeOverride.cs` (28)
  - `ClassDamageRateEntry.cs` (26), `CombatLimitsConfiguration.cs` (28), `CombatLimitsPlugIn.cs` (73)
  - `GuildLimitsPlugIn.cs` (138)
  - `MapRuleEntry.cs` (38), `MapRulesConfiguration.cs` (17), `MapRulesPlugIn.cs` (123)
  - `MonsterAiConfiguration.cs` (32), `MonsterAiPlugIn.cs` (47)
  - `MultiVaultAccountState.cs` (24), `MultiVaultConfiguration.cs` (41), `MultiVaultItemSnapshot.cs` (35), `MultiVaultOptionSnapshot.cs` (20), `MultiVaultPlugIn.cs` (471), `MultiVaultStorageSnapshot.cs` (17)
  - `PartyExpBonusEntry.cs` (18), `RateTuningConfiguration.cs` (87), `RateTuningPlugIn.cs` (284), `ZenTableEntry.cs` (18)
- **Suite references**:
  - Season 6 & Season 9 `IGCData/IGC_Common.ini` (`GuildCreateLevel`, `MaxGuildMember`, `AllianceMinGuildMember`, `AllianceMaxGuilds`, `CastleOwnerGuildDestroyLimit`)
  - Season 6 & Season 9 `IGCData/IGC_ZenDrop.xml`
  - `docs/audit/findings/owner-answers-monsters-2026-11-21.md`

## Rules & Comparison

| Rule / Behaviour | HNMO | Season 6 / Season 9 Suite | Match |
|---|---|---|---|
| Guild creation level & member cap (`GuildLimitsPlugIn`) | `MinimumCreateLevel = 150`, `MaximumMembers = 80`, `MinimumAllianceGuildMembers = 10`; master `level / 10` cap omitted per owner decision (20 Nov 2026) | S6 & S9 `IGC_Common.ini`: `GuildCreateLevel = 150`, `MaxGuildMember = 80`, `AllianceMinGuildMember = 10` (`AllianceMaxGuilds = 3`, `CastleOwnerGuildDestroyLimit = 1` not enforced here) | Same for create level, max members, min alliance members; `AllianceMaxGuilds`/`CastleOwnerGuildDestroyLimit` checked in guild actions |
| Monster AI & kill attribution (`MonsterAiPlugIn` / `MonsterAiConfiguration`) | Default on: `ChaseAttacker = true`, `LeashToSpawnArea = true`, `LeashExtraRange = 2`, `HealToFullOnLeash = true`, `SplitExpByDamage = true`, `DropToTopDamageDealer = true` (`Off` sets all `false`) | Standard chase, leash, full heal on return, damage-proportional EXP & top-damage drop | Same |
| Party EXP bonus table (`RateTuningPlugIn.DefaultPartyExpBonuses`) | 2: 160%, 3: 180% (230% diff classes), 4: 200% (270% diff classes), 5: 220% (300% diff classes) | Standard S6/S9 party EXP & set-party EXP curve | Same |
| Life option cap & Jewel/Chaos/Zen tuning (`RateTuningPlugIn`, `ChaosRatesPlugIn`) | `MaxLifeOptionLevel = 7` (+28 / 7% rec), optional `OverrideSoulRates`/`OverrideLifeRate`, `ZenTable` (`IGC_ZenDrop.xml` pattern), `ClassDamageRates`, `ChaosRatesPlugIn` global + per-recipe overrides | S6/S9 `MaxLifeOption = 7`; per-map zen table in `IGC_ZenDrop.xml` | Same |
| Bonus events, Map rules, Combat limits, Multi-vault (`BonusEventsPlugIn`, `MapRulesPlugIn`, `CombatLimitsPlugIn`, `MultiVaultPlugIn`) | HNMO host-configurable features (`BonusEvents`, `MapRules`, `CombatLimits` off by default; `MultiVault` on by default: 2 free + 1 VIP + coin-bought up to 5) | HNMO admin/QOL extensions | HNMO-specific |
