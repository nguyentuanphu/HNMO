// <copyright file="LevelDependentDamage.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.DataModel.Configuration;

using HNMO.Annotations;

/// <summary>
/// Defines a level-dependent damage.
/// </summary>
[Cloneable]
public partial class LevelDependentDamage
{
    /// <summary>
    /// Gets or sets the level belonging to this damage value.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets the damage belonging to this level.
    /// </summary>
    public int Damage { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Format(HNMO.DataModel.Properties.Resources.LevelValueSummary, this.Level, this.Damage);
    }
}