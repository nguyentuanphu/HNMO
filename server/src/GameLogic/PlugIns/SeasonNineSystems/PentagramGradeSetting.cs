// <copyright file="PentagramGradeSetting.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

/// <summary>The grade of a pentagram (admin setting): it has grade + 2 errtel slots, at most five.</summary>
public sealed class PentagramGradeSetting
{
    /// <summary>Gets or sets the pentagram's item number (12/200..214).</summary>
    public short PentagramNumber { get; set; }

    /// <summary>Gets or sets the grade (1..3).</summary>
    public byte Grade { get; set; }
}
