// <copyright file="ItemLevelPrice.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.DataModel.Configuration.Items;

using HNMO.Annotations;

/// <summary>
/// Defines a fixed base price of an item at a specific item level.
/// </summary>
[Cloneable]
public partial class ItemLevelPrice
{
    /// <summary>
    /// Gets or sets the item level.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets the base price of one unit of the item at this <see cref="Level"/>.
    /// </summary>
    public long Price { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"+{this.Level}: {this.Price}";
    }
}
