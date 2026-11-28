// <copyright file="WorldPublicSummary.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Worlds;

/// <summary>
/// Player-facing grouped world summary for the website and server selector (ROADMAP 15.7).
/// </summary>
public sealed class WorldPublicSummary
{
    /// <summary>Gets or sets the world slug identifier.</summary>
    public string WorldId { get; set; } = string.Empty;

    /// <summary>Gets or sets the English name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese name.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the season's key: "s6" or "s9".</summary>
    public string Season { get; set; } = "s6";

    /// <summary>Gets or sets the season's badge for players: "S6.3" or "S9".</summary>
    public string SeasonBadge { get; set; } = "S6.3";

    /// <summary>Gets or sets a value indicating whether the world is running.</summary>
    public bool IsRunning { get; set; }

    /// <summary>Gets or sets a value indicating whether the world has opened (based on Sydney opening time).</summary>
    public bool IsOpen { get; set; }

    /// <summary>Gets or sets the scheduled opening timestamp in UTC.</summary>
    public DateTimeOffset? OpeningDateUtc { get; set; }

    /// <summary>Gets or sets the formatted Sydney opening timestamp.</summary>
    public string OpeningDateSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets the EXP multiplier.</summary>
    public double ExperienceMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets the master EXP multiplier.</summary>
    public double MasterExperienceMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets the item drop multiplier.</summary>
    public double DropMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets the zen drop multiplier.</summary>
    public double ZenMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets the English event preset label.</summary>
    public string EventPresetEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese event preset label.</summary>
    public string EventPresetVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the English text of the game's server choice for this world (empty: the game's own).</summary>
    public string ServerChoiceTextEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese text of the game's server choice for this world (empty: the game's own).</summary>
    public string ServerChoiceTextVi { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether one-off transfers into this world are allowed.</summary>
    public bool TransferEnabled { get; set; }

    /// <summary>Gets or sets the total online players across servers in this world.</summary>
    public int TotalPlayers { get; set; }

    /// <summary>Gets or sets the total capacity across servers in this world.</summary>
    public int TotalCapacity { get; set; }

    /// <summary>Gets or sets the servers grouped under this world.</summary>
    public List<WorldServerEntry> Servers { get; set; } = [];
}
