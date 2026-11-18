// <copyright file="CustomRankEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CustomRanks;

/// <summary>
/// Pure rule engine for evaluating which custom rank tiers a character unlocks and which badge displays.
/// </summary>
public sealed class CustomRankEngine
{
    private readonly CustomRankConfiguration _config;
    private readonly CustomRankStore _store;

    /// <summary>Initializes a new instance of the <see cref="CustomRankEngine"/> class.</summary>
    /// <param name="config">Plug-in configuration.</param>
    /// <param name="store">Character preference and achievement store.</param>
    public CustomRankEngine(CustomRankConfiguration config, CustomRankStore store)
    {
        this._config = config;
        this._store = store;
    }

    /// <summary>Determines whether <paramref name="subject"/> satisfies every requirement on <paramref name="tier"/>.</summary>
    /// <param name="tier">The tier definition.</param>
    /// <param name="subject">Current character metrics.</param>
    /// <param name="record">Optional persisted achievement record.</param>
    /// <returns><c>true</c> if unlocked.</returns>
    public static bool Qualifies(
        CustomRankTierConfig tier,
        CustomRankSubject subject,
        CustomRankCharacterRecord? record)
    {
        if (!tier.Enabled || string.IsNullOrWhiteSpace(tier.Id))
        {
            return false;
        }

        if (tier.MinLevel > 0 && subject.Level < tier.MinLevel)
        {
            return false;
        }

        if (tier.MinMasterLevel > 0 && subject.MasterLevel < tier.MinMasterLevel)
        {
            return false;
        }

        if (tier.MinResets > 0 && subject.Resets < tier.MinResets)
        {
            return false;
        }

        if (tier.MinVipLevel > 0 && subject.VipLevel < tier.MinVipLevel)
        {
            return false;
        }

        if (tier.MinMissionsClaimed > 0 && subject.MissionsClaimed < tier.MinMissionsClaimed)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(tier.RequiredAchievementKey))
        {
            var required = tier.RequiredAchievementKey.Trim();
            var hasKey = record?.AchievementKeys.Any(k => string.Equals(k, required, StringComparison.OrdinalIgnoreCase)) ?? false;
            if (!hasKey)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns all unlocked tiers for <paramref name="subject"/> ordered by descending priority.</summary>
    /// <param name="subject">Current character metrics.</param>
    /// <returns>Unlocked tiers in display priority order.</returns>
    public IReadOnlyList<CustomRankTierConfig> GetUnlockedTiers(CustomRankSubject subject)
    {
        var record = this._store.GetCharacter(subject.CharacterName);
        return (this._config.Tiers ?? [])
            .Where(t => Qualifies(t, subject, record))
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Resolves the active tier for a character (player choice if still unlocked, otherwise highest priority unlocked).</summary>
    /// <param name="subject">Current character metrics.</param>
    /// <returns>The active tier or <c>null</c> when none qualify or the feature is disabled.</returns>
    public CustomRankTierConfig? ResolveActiveTier(CustomRankSubject subject)
    {
        var unlocked = this.GetUnlockedTiers(subject);
        if (unlocked.Count == 0)
        {
            return null;
        }

        var record = this._store.GetCharacter(subject.CharacterName);
        if (record is not null && !string.IsNullOrWhiteSpace(record.SelectedRankId))
        {
            var chosen = unlocked.FirstOrDefault(t => string.Equals(t.Id, record.SelectedRankId, StringComparison.OrdinalIgnoreCase));
            if (chosen is not null)
            {
                return chosen;
            }
        }

        return unlocked[0];
    }

    /// <summary>Updates the character's equipped tier (pass empty or <c>"auto"</c> to clear manual selection).</summary>
    /// <param name="subject">Current character metrics.</param>
    /// <param name="rankId">Requested rank tier id.</param>
    /// <param name="errorEn">Localized English error if denied.</param>
    /// <param name="errorVi">Localized Vietnamese error if denied.</param>
    /// <returns><c>true</c> if accepted.</returns>
    public bool TrySelectRank(
        CustomRankSubject subject,
        string? rankId,
        out string errorEn,
        out string errorVi)
    {
        var clean = (rankId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(clean) || string.Equals(clean, "auto", StringComparison.OrdinalIgnoreCase))
        {
            this._store.SetSelection(subject.CharacterName, string.Empty);
            errorEn = string.Empty;
            errorVi = string.Empty;
            return true;
        }

        var unlocked = this.GetUnlockedTiers(subject);
        var match = unlocked.FirstOrDefault(t => string.Equals(t.Id, clean, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            errorEn = $"Rank '{clean}' is locked or unknown.";
            errorVi = $"Danh hiệu '{clean}' chưa được mở khóa hoặc không tồn tại.";
            return false;
        }

        this._store.SetSelection(subject.CharacterName, match.Id);
        errorEn = string.Empty;
        errorVi = string.Empty;
        return true;
    }
}
