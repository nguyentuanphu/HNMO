// <copyright file="ResetItemCost.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using HNMO.DataModel.Configuration;

/// <summary>
/// An item a reset or relife takes from the bag.
/// </summary>
public class ResetItemCost
{
    /// <summary>Gets or sets the item group.</summary>
    [Range(0, 15)]
    public short Group { get; set; } = 14;

    /// <summary>Gets or sets the item number in its group.</summary>
    [Range(0, 511)]
    public short Number { get; set; } = 13;

    /// <summary>Gets or sets the lowest item level which counts.</summary>
    [Range(0, 15)]
    public byte MinimumLevel { get; set; }

    /// <summary>Gets or sets how many pieces are taken.</summary>
    [Range(1, 100)]
    public int Count { get; set; } = 1;
}
