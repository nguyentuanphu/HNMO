// <copyright file="LuckyNumberRound.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.LuckyNumber;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;

/// <summary>
/// A daily lucky number round (identified by <c>yyyy-MM-dd</c> draw date in Sydney/configured time zone).
/// </summary>
public sealed class LuckyNumberRound
{
    /// <summary>Gets or sets the draw date key (<c>yyyy-MM-dd</c>).</summary>
    public string DateKey { get; set; } = string.Empty;

    /// <summary>Gets or sets all picks submitted for this round.</summary>
    public List<LuckyNumberPickEntry> Picks { get; set; } = [];

    /// <summary>Gets or sets the drawn winning number, or <c>null</c> until drawn.</summary>
    public int? DrawnNumber { get; set; }

    /// <summary>Gets or sets the UTC draw timestamp.</summary>
    public DateTime? DrawnAtUtc { get; set; }

    /// <summary>Gets or sets the winners matched during the draw.</summary>
    public List<LuckyNumberWinnerEntry> Winners { get; set; } = [];
}
