// <copyright file="SeasonPlanEngine.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonPlan;

/// <summary>
/// Pure rules &amp; condition evaluator for Season Plan progression stages.
/// </summary>
public sealed class SeasonPlanEngine
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonPlanEngine"/> class.
    /// </summary>
    /// <param name="configuration">Season Plan configuration.</param>
    /// <param name="store">Persistent store.</param>
    /// <param name="enabled">Whether the plug-in is active.</param>
    public SeasonPlanEngine(SeasonPlanConfiguration configuration, SeasonPlanStore store, bool enabled)
    {
        this.Configuration = configuration;
        this.Store = store;
        this.Enabled = enabled;
    }

    /// <summary>Gets the configuration.</summary>
    public SeasonPlanConfiguration Configuration { get; }

    /// <summary>Gets the atomic store.</summary>
    public SeasonPlanStore Store { get; }

    /// <summary>Gets a value indicating whether Season Plan enforcement is enabled.</summary>
    public bool Enabled { get; }

    /// <summary>Evaluates scheduled dates, boss kills, and level milestones, unlocking any newly satisfied stages.</summary>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>List of stages newly unlocked during this evaluation.</returns>
    public IReadOnlyList<SeasonStageDefinition> EvaluateConditions(DateTime utcNow)
    {
        if (!this.Enabled)
        {
            return [];
        }

        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var newlyUnlocked = new List<SeasonStageDefinition>();
        this.Store.Mutate(data =>
        {
            var changed = false;
            foreach (var stage in this.Configuration.Stages)
            {
                if (string.IsNullOrWhiteSpace(stage.Id) || data.UnlockedStages.ContainsKey(stage.Id))
                {
                    continue;
                }

                string? reason = null;
                if (stage.UnlockAtUtc is { } atUtc && now >= DateTime.SpecifyKind(atUtc, DateTimeKind.Utc))
                {
                    reason = "date";
                }
                else if (!string.IsNullOrWhiteSpace(stage.RequiredBossId)
                         && data.BossKills.TryGetValue(stage.RequiredBossId.Trim(), out var kills)
                         && kills >= Math.Max(1, stage.RequiredBossKills))
                {
                    reason = "boss";
                }
                else if (stage.RequiredPlayerLevel > 0
                         && stage.RequiredPlayersCount > 0
                         && data.CharacterLevels.Values.Count(lvl => lvl >= stage.RequiredPlayerLevel) >= stage.RequiredPlayersCount)
                {
                    reason = "players";
                }

                if (reason is not null)
                {
                    data.UnlockedStages[stage.Id] = now;
                    data.UnlockReasons[stage.Id] = reason;
                    newlyUnlocked.Add(stage);
                    changed = true;
                }
            }

            return (changed, changed);
        });

        return newlyUnlocked;
    }

    /// <summary>Records a World Boss kill and evaluates stage unlocks.</summary>
    /// <param name="bossId">Boss ID.</param>
    /// <param name="utcNow">UTC timestamp.</param>
    /// <returns>Newly unlocked stages.</returns>
    public IReadOnlyList<SeasonStageDefinition> ObserveBossKill(string bossId, DateTime utcNow)
    {
        if (!this.Enabled || string.IsNullOrWhiteSpace(bossId))
        {
            return [];
        }

        var key = bossId.Trim();
        this.Store.Mutate(data =>
        {
            data.BossKills.TryGetValue(key, out var cur);
            data.BossKills[key] = cur + 1;
            return (true, cur + 1);
        });

        return this.EvaluateConditions(utcNow);
    }

    /// <summary>Records a character's level and evaluates stage unlocks.</summary>
    /// <param name="characterId">Character ID.</param>
    /// <param name="level">Character level.</param>
    /// <param name="utcNow">UTC timestamp.</param>
    /// <returns>Newly unlocked stages.</returns>
    public IReadOnlyList<SeasonStageDefinition> ObserveCharacterLevel(string characterId, int level, DateTime utcNow)
    {
        if (!this.Enabled || string.IsNullOrWhiteSpace(characterId) || level <= 0)
        {
            return [];
        }

        var updated = this.Store.Mutate(data =>
        {
            data.CharacterLevels.TryGetValue(characterId, out var prev);
            if (level <= prev)
            {
                return (false, false);
            }

            data.CharacterLevels[characterId] = level;
            return (true, true);
        });

        return updated ? this.EvaluateConditions(utcNow) : [];
    }

    /// <summary>Manually unlocks or locks a stage from the admin panel.</summary>
    /// <param name="stageId">Stage ID.</param>
    /// <param name="unlocked">Whether to unlock or re-lock.</param>
    /// <param name="utcNow">UTC timestamp.</param>
    public void SetStageUnlocked(string stageId, bool unlocked, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            return;
        }

        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        this.Store.Mutate(data =>
        {
            if (unlocked)
            {
                data.UnlockedStages[stageId] = now;
                data.UnlockReasons[stageId] = "admin";
            }
            else
            {
                data.UnlockedStages.Remove(stageId);
                data.UnlockReasons.Remove(stageId);
            }

            return (true, true);
        });
    }

    /// <summary>Returns whether a given gameplay feature gate is unlocked at <paramref name="utcNow"/>.</summary>
    /// <param name="gate">Feature gate.</param>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns><c>true</c> if feature is enabled/unlocked.</returns>
    public bool IsFeatureUnlocked(SeasonFeatureGate gate, DateTime utcNow)
    {
        if (!this.Enabled)
        {
            return true;
        }

        this.EvaluateConditions(utcNow);
        var declaringStages = this.Configuration.Stages.Where(s => s.Unlocks.Contains(gate)).ToList();
        if (declaringStages.Count == 0)
        {
            // Feature isn't gated by any configured stage -> allowed.
            return true;
        }

        return this.Store.Read(data => declaringStages.Any(s => data.UnlockedStages.ContainsKey(s.Id)));
    }

    /// <summary>Gets the current highest <see cref="SeasonStageDefinition.MaxBoxLevel"/> across all unlocked stages (0 if unrestricted/disabled).</summary>
    /// <param name="utcNow">Current UTC timestamp.</param>
    /// <returns>Max allowed box level, or 0 if no unlocked stage defines one.</returns>
    public int GetCurrentMaxBoxLevel(DateTime utcNow)
    {
        if (!this.Enabled)
        {
            return 0;
        }

        this.EvaluateConditions(utcNow);
        return this.Store.Read(data =>
        {
            var max = 0;
            foreach (var s in this.Configuration.Stages)
            {
                if (data.UnlockedStages.ContainsKey(s.Id) && s.MaxBoxLevel > max)
                {
                    max = s.MaxBoxLevel;
                }
            }

            return max;
        });
    }
}
