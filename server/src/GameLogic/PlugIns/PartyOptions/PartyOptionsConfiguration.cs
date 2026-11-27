// <copyright file="PartyOptionsConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PartyOptions;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration for <see cref="PartyOptionsPlugIn"/> (OFF by default):
/// password party join (<c>/partypass</c>) and Auto-Party finder for hunting spots.
/// </summary>
public sealed class PartyOptionsConfiguration
{
    /// <summary>Gets or sets shared HNMO state key.</summary>
    [StringLength(260)]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets a value indicating whether <c>/partypass</c> and password join are active.</summary>
    public bool AllowPasswordJoin { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether Auto-Party hunting spot listings are active.</summary>
    public bool AllowAutoSpotParty { get; set; } = true;

    /// <summary>Gets or sets max characters allowed for a party password.</summary>
    [Range(2, 24)]
    public int MaxPasswordLength { get; set; } = 16;

    /// <summary>Gets or sets suggested hunting spots displayed in the Party window drawer &amp; website.</summary>
    public List<string> HuntingSpots { get; set; } =
    [
        "Lorencia",
        "Devias",
        "LostTower 7",
        "Atlans 3",
        "Tarkan 2",
        "Icarus",
        "Kanturu 3",
    ];
}
