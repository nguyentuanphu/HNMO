// <copyright file="StarterKitBuffConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The temporary EXP buff of the starter kit: the effect of an item of the game's data (by default the Seal of Ascension).
/// </summary>
public class StarterKitBuffConfiguration
{
    /// <summary>Gets or sets a value indicating whether the kit gives the buff.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the group of the item whose effect is given.</summary>
    [Range(0, 15)]
    public short ItemGroup { get; set; } = 13;

    /// <summary>Gets or sets the number of the item whose effect is given.</summary>
    [Range(0, 511)]
    public short ItemNumber { get; set; } = 43;

    /// <summary>Gets or sets how long it lasts, in minutes.</summary>
    [Range(1, 10080)]
    public int Minutes { get; set; } = 60;
}
