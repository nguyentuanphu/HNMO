// <copyright file="TeleportDestinationConfig.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TeleportNpc;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Admin-defined map destination offered by <see cref="TeleportNpcPlugIn"/>.
/// </summary>
public sealed class TeleportDestinationConfig
{
    /// <summary>Gets or sets the unique stable identifier (e.g. <c>devias</c>).</summary>
    [Display(Name = "Id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether this destination appears in the Teleport NPC menu.</summary>
    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the English display name.</summary>
    [Display(Name = "Name (EN)")]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese display name.</summary>
    [Display(Name = "Name (VI)")]
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the destination game map number (e.g. <c>0</c> = Lorencia, <c>2</c> = Devias).</summary>
    [Display(Name = "Map Number")]
    public short MapNumber { get; set; }

    /// <summary>Gets or sets the destination X cell coordinate.</summary>
    [Display(Name = "Target X")]
    public byte TargetX { get; set; }

    /// <summary>Gets or sets the destination Y cell coordinate.</summary>
    [Display(Name = "Target Y")]
    public byte TargetY { get; set; }

    /// <summary>Gets or sets the minimum character level required (<c>0</c> = none).</summary>
    [Display(Name = "Min Level")]
    public int MinLevel { get; set; }

    /// <summary>Gets or sets the maximum character level allowed (<c>0</c> = no upper limit).</summary>
    [Display(Name = "Max Level")]
    public int MaxLevel { get; set; }

    /// <summary>Gets or sets the minimum reset count required (<c>0</c> = none).</summary>
    [Display(Name = "Min Resets")]
    public int MinResets { get; set; }

    /// <summary>Gets or sets the Zen cost charged per warp (<c>0</c> = free).</summary>
    [Display(Name = "Zen Cost")]
    public int ZenCost { get; set; }

    /// <summary>Gets or sets a value indicating whether VIP status is required.</summary>
    [Display(Name = "VIP Only")]
    public bool VipOnly { get; set; }

    /// <summary>Gets or sets a value indicating whether killer/PK players are permitted to use this warp.</summary>
    [Display(Name = "Allow PK")]
    public bool AllowPk { get; set; } = true;
}
