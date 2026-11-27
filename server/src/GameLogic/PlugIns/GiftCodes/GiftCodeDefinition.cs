// <copyright file="GiftCodeDefinition.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GiftCodes;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.TopRaces;

/// <summary>
/// A gift code, or a batch of single-use codes which share one reward and its rules. The codes are kept in the gift
/// codes' file in the save folder (not in the database), with their uses and the redemption log.
/// </summary>
public class GiftCodeDefinition
{
    /// <summary>Gets or sets the identifier (made when it's created; references and the log use it).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the code players enter (empty for a batch). Matched without regard to case.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the codes of a batch; each one can be used once (by anybody, within the other rules).</summary>
    public List<string> BatchCodes { get; set; } = new();

    /// <summary>Gets or sets the prefix the batch's codes were made with.</summary>
    public string BatchPrefix { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the code works.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the code is shown on the website (only a single code, not a batch).</summary>
    public bool Public { get; set; }

    /// <summary>Gets or sets the admin's note (also the title shown with a public code).</summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>Gets or sets the reward.</summary>
    public GiftCodeReward Reward { get; set; } = new();

    /// <summary>Gets or sets how many times the code (all codes of a batch together) can be used; 0 is no limit.</summary>
    [Range(0, 10_000_000)]
    public int MaxUses { get; set; }

    /// <summary>Gets or sets who can use it once.</summary>
    public GiftCodeLimit Limit { get; set; } = GiftCodeLimit.OncePerAccount;

    /// <summary>Gets or sets a value indicating whether this code may be used once per network address.</summary>
    public bool OncePerNetwork { get; set; }

    /// <summary>Gets or sets a value indicating whether this code may be used once per known browser device.</summary>
    public bool OncePerDevice { get; set; }

    /// <summary>Gets or sets the start, "yyyy-MM-dd HH:mm" on the wall clock of the time zone (Sydney); empty is no start.</summary>
    public string Start { get; set; } = string.Empty;

    /// <summary>Gets or sets the end, "yyyy-MM-dd HH:mm" on the wall clock of the time zone (Sydney); empty is no end.</summary>
    public string End { get; set; } = string.Empty;

    /// <summary>Gets or sets the lowest character level; 0 is no limit.</summary>
    [Range(0, 1000)]
    public int MinLevel { get; set; }

    /// <summary>Gets or sets the highest character level; 0 is no limit.</summary>
    [Range(0, 1000)]
    public int MaxLevel { get; set; }

    /// <summary>Gets or sets the fewest resets; 0 is no limit.</summary>
    [Range(0, 100_000)]
    public int MinResets { get; set; }

    /// <summary>Gets or sets the most resets; -1 is no limit (0 means characters which never reset).</summary>
    [Range(-1, 100_000)]
    public int MaxResets { get; set; } = -1;

    /// <summary>Gets or sets the classes (with their later evolutions) which can use it; empty is every class.</summary>
    public List<TopRaceClass> Classes { get; set; } = new();

    /// <summary>Gets or sets when it was created.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Gets a value indicating whether it's a batch of single-use codes.</summary>
    [JsonIgnore]
    public bool IsBatch => this.BatchCodes.Count > 0;

    /// <summary>Gets the text which names it in lists: the code, or the batch's prefix and size.</summary>
    [JsonIgnore]
    public string DisplayCode => this.IsBatch ? $"{this.BatchPrefix}… ({this.BatchCodes.Count})" : this.Code;
}
