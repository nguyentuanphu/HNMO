// <copyright file="PentagramMonsterElement.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

/// <summary>The Season 9 element of a monster (admin setting).</summary>
public sealed class PentagramMonsterElement
{
    /// <summary>Gets or sets the monster definition number.</summary>
    public int MonsterNumber { get; set; }

    /// <summary>Gets or sets the element.</summary>
    public PentagramElement Element { get; set; }
}
