// <copyright file="MiniGameTerrainChange.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.DataModel.Configuration;

using HNMO.Annotations;

/// <summary>
/// Defines a terrain change of the mini game map.
/// </summary>
[Cloneable]
public partial class MiniGameTerrainChange
{
    /// <summary>
    /// Gets or sets the type of terrain attribute which should be added or removed to or from the terrain.
    /// </summary>
    public TerrainAttributeType TerrainAttribute { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to set or to remove the <see cref="TerrainAttribute"/> from the terrain.
    /// </summary>
    public bool SetTerrainAttribute { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether no client update is required.
    /// </summary>
    public bool IsClientUpdateRequired { get; set; }

    /// <summary>
    /// Gets or sets the start value of the X-coordinate of the terrain area.
    /// </summary>
    public byte StartX { get; set; }

    /// <summary>
    /// Gets or sets the start value of the Y-coordinate of the terrain area.
    /// </summary>
    public byte StartY { get; set; }

    /// <summary>
    /// Gets or sets the end value of the X-coordinate of the terrain area.
    /// </summary>
    public byte EndX { get; set; }

    /// <summary>
    /// Gets or sets the end value of the Y-coordinate of the terrain area.
    /// </summary>
    public byte EndY { get; set; }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Format(
            HNMO.DataModel.Properties.Resources.TerrainChangeSummary,
            this.SetTerrainAttribute ? HNMO.DataModel.Properties.Resources.TerrainSet : HNMO.DataModel.Properties.Resources.TerrainRemove,
            ModelResourceProvider.GetEnumCaption(this.TerrainAttribute.GetType(), this.TerrainAttribute),
            this.StartX,
            this.StartY,
            this.EndX,
            this.EndY,
            this.IsClientUpdateRequired ? HNMO.DataModel.Properties.Resources.TerrainWithClientUpdate : HNMO.DataModel.Properties.Resources.TerrainWithoutClientUpdate);
    }
}