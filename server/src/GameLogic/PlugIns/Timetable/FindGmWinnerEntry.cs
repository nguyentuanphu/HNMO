// <copyright file="FindGmWinnerEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Timetable;

using System.IO;
using System.Text.Json;

/// <summary>
/// Recorded winner of a Find the GM event run.
/// </summary>
public sealed class FindGmWinnerEntry
{
    /// <summary>Gets or sets the winning character name.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the map where the GM was found.</summary>
    public string MapName { get; set; } = string.Empty;

    /// <summary>Gets or sets the coins awarded.</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public string CoinType { get; set; } = string.Empty;

    /// <summary>Gets or sets the zen awarded.</summary>
    public int Zen { get; set; }

    /// <summary>Gets or sets the UTC claim timestamp.</summary>
    public DateTime WonAtUtc { get; set; }
}
