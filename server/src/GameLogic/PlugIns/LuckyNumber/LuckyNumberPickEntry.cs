// <copyright file="LuckyNumberPickEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.LuckyNumber;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;

/// <summary>
/// A single player pick recorded for a daily draw round.
/// </summary>
public sealed class LuckyNumberPickEntry
{
    /// <summary>Gets or sets the normalized account login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the character name (or login name when picked from website).</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the chosen number.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the UTC pick timestamp.</summary>
    public DateTime PickedAtUtc { get; set; }

    /// <summary>Gets or sets the source (<c>game</c> or <c>website</c>).</summary>
    public string Source { get; set; } = "game";
}
