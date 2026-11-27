// <copyright file="MarriageCoupleEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Marriage;

using System.IO;
using System.Text.Json;

/// <summary>
/// Persisted married couple record.
/// </summary>
public sealed class MarriageCoupleEntry
{
    /// <summary>Gets or sets the unique couple identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets partner A's character name.</summary>
    public string PartnerAName { get; set; } = string.Empty;

    /// <summary>Gets or sets partner A's account login.</summary>
    public string PartnerALogin { get; set; } = string.Empty;

    /// <summary>Gets or sets partner B's character name.</summary>
    public string PartnerBName { get; set; } = string.Empty;

    /// <summary>Gets or sets partner B's account login.</summary>
    public string PartnerBLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets optional shared custom couple title.</summary>
    public string CustomTitle { get; set; } = string.Empty;

    /// <summary>Gets or sets ceremony UTC timestamp.</summary>
    public DateTime MarriedAtUtc { get; set; }
}
