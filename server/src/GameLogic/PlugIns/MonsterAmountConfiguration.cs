// <copyright file="MonsterAmountConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Composition;

/// <summary>
/// The configuration of the <see cref="MonsterAmountPlugIn"/>: a multiplier of the monster amounts per map.
/// The stored quantities of the spawn areas stay as they are; the multiplier is applied when the monsters spawn.
/// </summary>
public class MonsterAmountConfiguration
{
    /// <summary>
    /// The smallest multiplier.
    /// </summary>
    public const double MinimumMultiplier = 0.5;

    /// <summary>
    /// The biggest multiplier.
    /// </summary>
    public const double MaximumMultiplier = 5;

    /// <summary>
    /// Gets the multipliers which the admin panel offers.
    /// </summary>
    public static IReadOnlyList<double> Choices { get; } = [0.5, 1, 2, 3, 4, 5];

    /// <summary>
    /// Gets or sets the multipliers of the maps; maps which aren't listed have the multiplier 1.
    /// </summary>
    [MemberOfAggregate]
    [ScaffoldColumn(true)]
    [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MonsterAmountConfiguration_Maps_Name))]
    public ICollection<MapMonsterAmount> Maps { get; set; } = new List<MapMonsterAmount>();

    /// <summary>
    /// Brings a multiplier into the allowed range; an invalid number counts as 1.
    /// </summary>
    /// <param name="multiplier">The multiplier.</param>
    /// <returns>The multiplier between <see cref="MinimumMultiplier"/> and <see cref="MaximumMultiplier"/>.</returns>
    public static double Normalize(double multiplier)
    {
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier <= 0)
        {
            return 1;
        }

        return Math.Clamp(multiplier, MinimumMultiplier, MaximumMultiplier);
    }

    /// <summary>
    /// Applies a multiplier to the quantity of a spawn area. The result is rounded (halves up),
    /// and a spawn area which has monsters keeps at least one.
    /// </summary>
    /// <param name="quantity">The stored quantity.</param>
    /// <param name="multiplier">The multiplier.</param>
    /// <returns>The quantity which spawns.</returns>
    public static int Apply(int quantity, double multiplier)
    {
        if (quantity <= 0)
        {
            return 0;
        }

        var normalized = Normalize(multiplier);
        if (normalized == 1)
        {
            return quantity;
        }

        var result = (int)Math.Round(quantity * normalized, MidpointRounding.AwayFromZero);
        return Math.Max(1, result);
    }

    /// <summary>
    /// Gets the multiplier of a map.
    /// </summary>
    /// <param name="mapNumber">The number of the map.</param>
    /// <returns>The multiplier; 1 if the map isn't listed.</returns>
    public double GetMultiplier(short mapNumber)
    {
        var entry = this.Maps.LastOrDefault(m => m.MapNumber == mapNumber);
        return entry is null ? 1 : Normalize(entry.Multiplier);
    }

    /// <summary>
    /// Sets the multiplier of a map; 1 removes the map's entry.
    /// </summary>
    /// <param name="mapNumber">The number of the map.</param>
    /// <param name="multiplier">The multiplier.</param>
    public void SetMultiplier(short mapNumber, double multiplier)
    {
        var normalized = Normalize(multiplier);
        foreach (var entry in this.Maps.Where(m => m.MapNumber == mapNumber).ToList())
        {
            this.Maps.Remove(entry);
        }

        if (normalized != 1)
        {
            this.Maps.Add(new MapMonsterAmount { MapNumber = mapNumber, Multiplier = normalized });
        }
    }

    /// <summary>
    /// The multiplier of a map.
    /// </summary>
    public class MapMonsterAmount
    {
        /// <summary>
        /// Gets or sets the number of the map.
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MonsterAmountConfiguration_MapNumber_Name))]
        public short MapNumber { get; set; }

        /// <summary>
        /// Gets or sets the multiplier of the monster amounts (0.5 to 5).
        /// </summary>
        [Display(ResourceType = typeof(PlugInResources), Name = nameof(PlugInResources.MonsterAmountConfiguration_Multiplier_Name))]
        [Range(MinimumMultiplier, MaximumMultiplier)]
        public double Multiplier { get; set; } = 1;

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{this.MapNumber}: ×{this.Multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }
    }
}
