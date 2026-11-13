// <copyright file="CastleSiegeZoneDefinition.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.DataModel.Configuration;

using HNMO.Annotations;

/// <summary>
/// Defines a rectangular zone on the castle siege map, used for spawn areas and machine zones.
/// </summary>
[Cloneable]
public partial class CastleSiegeZoneDefinition
{
    /// <summary>
    /// Gets or sets the top-left X coordinate of the zone.
    /// </summary>
    public byte X1 { get; set; }

    /// <summary>
    /// Gets or sets the top-left Y coordinate of the zone.
    /// </summary>
    public byte Y1 { get; set; }

    /// <summary>
    /// Gets or sets the bottom-right X coordinate of the zone.
    /// </summary>
    public byte X2 { get; set; }

    /// <summary>
    /// Gets or sets the bottom-right Y coordinate of the zone.
    /// </summary>
    public byte Y2 { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Format(Properties.Resources.CastleSiegeZoneSummary, this.X1, this.Y1, this.X2, this.Y2);
    }
}
