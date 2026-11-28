// <copyright file="MuunState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineSystems;

/// <summary>The Season 9 Muun state of a character, kept in its state row (<see cref="MuunCompanion.FeatureKey"/>).</summary>
public sealed class MuunState
{
    /// <summary>Gets or sets the identifier of the active Muun item (in the bag), or <c>null</c>.</summary>
    public Guid? ActiveItemId { get; set; }

    /// <summary>Gets or sets the energy from converted Muuns, spent on level ups.</summary>
    public int Energy { get; set; }
}
