// <copyright file="CommunityDropFilter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// Which dropped items count as rare.
/// </summary>
public class CommunityDropFilter
{
    /// <summary>Gets or sets a value indicating whether excellent items count.</summary>
    public bool Excellent { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether ancient (set) items count.</summary>
    public bool Ancient { get; set; } = true;

    /// <summary>Gets or sets the item level from which an item counts; 0 for none.</summary>
    [Range(0, 15)]
    public int MinimumLevel { get; set; }

    /// <summary>Gets or sets the items which always count, as "group number" (e.g. "14 13" for a Jewel of Bless).</summary>
    public ICollection<string> Items { get; set; } = new List<string>();

    /// <summary>Gets or sets the map numbers on which drops are posted; empty for every map.</summary>
    public ICollection<short> Maps { get; set; } = new List<short>();
}
