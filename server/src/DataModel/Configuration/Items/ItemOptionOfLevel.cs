// <copyright file="ItemOptionOfLevel.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.DataModel.Configuration.Items;

using HNMO.Annotations;
using HNMO.DataModel.Attributes;

/// <summary>
/// The item option, depending on the specified item level.
/// </summary>
[Cloneable]
public partial class ItemOptionOfLevel
{
    /// <summary>
    /// Gets or sets the level.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets the required item level.
    /// </summary>
    public int RequiredItemLevel { get; set; }

    /// <summary>
    /// Gets or sets the power up definition.
    /// </summary>
    [MemberOfAggregate]
    public virtual PowerUpDefinition? PowerUpDefinition { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Format(HNMO.DataModel.Properties.Resources.LevelValueSummary, this.Level, this.PowerUpDefinition);
    }
}