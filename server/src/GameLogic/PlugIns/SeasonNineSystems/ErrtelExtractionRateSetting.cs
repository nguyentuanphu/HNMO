// <copyright file="ErrtelExtractionRateSetting.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

/// <summary>The chance (n/10000) to take an errtel of a rank and level out of a pentagram intact (admin setting).</summary>
public sealed class ErrtelExtractionRateSetting
{
    /// <summary>Gets or sets the rank (1..5).</summary>
    public byte Rank { get; set; }

    /// <summary>Gets or sets the level (0..10).</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets the chance out of 10000.</summary>
    public int Rate { get; set; }
}
