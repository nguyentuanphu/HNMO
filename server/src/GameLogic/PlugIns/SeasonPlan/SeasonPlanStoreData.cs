// <copyright file="SeasonPlanStoreData.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonPlan;

using System.IO;
using System.Text.Json;

/// <summary>
/// Runtime progress state persisted under section <c>seasonPlan</c> in <see cref="HnmoStateStore"/>.
/// </summary>
public sealed class SeasonPlanStoreData
{
    /// <summary>Gets or sets unlocked stage ID -> UTC timestamp when unlocked.</summary>
    public Dictionary<string, DateTime> UnlockedStages { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets unlock reason keyed by stage ID (<c>date</c>, <c>boss</c>, <c>players</c>, <c>admin</c>).</summary>
    public Dictionary<string, string> UnlockReasons { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets recorded kill counts per boss ID.</summary>
    public Dictionary<string, int> BossKills { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets highest recorded level per character ID.</summary>
    public Dictionary<string, int> CharacterLevels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
